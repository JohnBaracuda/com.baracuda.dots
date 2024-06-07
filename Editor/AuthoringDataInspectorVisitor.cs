using Unity.Entities;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Baracuda.DOTS.Editor
{
    /// <summary>
    ///     Checks if the type has any Entity fields. The visitation is done recursively in nested types.
    /// </summary>
    /// <remarks>The visitation ends when the first Entity field is found, or after traversing all the fields.</remarks>
    internal sealed class HasEntityFieldVisitor : IPropertyBagVisitor, IPropertyVisitor
    {
        public bool HasEntity;

        public void Reset()
        {
            HasEntity = false;
        }

        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> propertyBag, ref TContainer container)
        {
            foreach (var property in propertyBag.GetProperties(ref container))
            {
                if (property is Property<TContainer, Entity> && !property.HasAttribute<HideInInspector>())
                {
                    HasEntity = true;
                    break;
                }

                property.Accept(this, ref container);
            }
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var value = property.GetValue(ref container);
            PropertyContainer.TryAccept(this, ref value);
        }
    }

    /// <summary>
    ///     Builds the PropertyField structure which can be displayed in the inspector.
    /// </summary>
    internal sealed class PropertyFieldBuilderVisitor : IPropertyBagVisitor, IPropertyVisitor
    {
        private VisualElement _Root;
        private UnityEditor.SerializedProperty _AuthoringDataProperty;
        private UnityEditor.SerializedProperty _ReferencesProperty;
        private PropertyPath _PropertyPath;
        private int _ReferenceIndex;
        private readonly HasEntityFieldVisitor _EntityVisitor;

        public PropertyFieldBuilderVisitor()
            : this(null, null, null)
        {
        }

        public PropertyFieldBuilderVisitor(VisualElement root, UnityEditor.SerializedProperty authoringDataProperty, UnityEditor.SerializedProperty referencesProperty)
        {
            _EntityVisitor = new HasEntityFieldVisitor();

            Reset(root, authoringDataProperty, referencesProperty);
        }

        public void Reset(VisualElement root, UnityEditor.SerializedProperty authoringDataProperty, UnityEditor.SerializedProperty referencesProperty)
        {
            _AuthoringDataProperty = authoringDataProperty;
            _ReferencesProperty = referencesProperty;
            _Root = root;
            _ReferenceIndex = 0;
        }

        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> propertyBag, ref TContainer container)
        {
            foreach (var property in propertyBag.GetProperties(ref container))
            {
                if (property.HasAttribute<HideInInspector>())
                {
                    continue;
                }

                property.Accept(this, ref container);
            }
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var value = property.GetValue(ref container);

            if (property is Property<TContainer, Entity>)
            {
                // remap the Entity fields to a slot in the GameObject references array
                var pf = new UnityEditor.UIElements.PropertyField(_ReferencesProperty.GetArrayElementAtIndex(_ReferenceIndex++), UnityEditor.ObjectNames.NicifyVariableName(property.Name));
                _Root.Add(pf);

                return;
            }

            _PropertyPath = PropertyPath.AppendProperty(_PropertyPath, property);

            if (!TypeTraits<TValue>.IsContainer)
            {
                var pf = new UnityEditor.UIElements.PropertyField(_AuthoringDataProperty.FindPropertyRelative(_PropertyPath.ToString()));
                _Root.Add(pf);
            }
            else
            {
                // FIXME: calling this here makes the algorithm N^2. Move outside
                _EntityVisitor.Reset();
                PropertyContainer.Accept(_EntityVisitor, property.GetValue(ref container));
                if (!_EntityVisitor.HasEntity)
                {
                    _Root.Add(new UnityEditor.UIElements.PropertyField(_AuthoringDataProperty.FindPropertyRelative(_PropertyPath.ToString())));
                }
                else
                {
                    var foldout = new Foldout {text = UnityEditor.ObjectNames.NicifyVariableName(property.Name)};
                    _Root.Add(foldout);
                    var currentRoot = _Root;
                    _Root = foldout;

                    PropertyContainer.Accept(this, ref value);

                    _Root = currentRoot;
                }
            }

            _PropertyPath = PropertyPath.Pop(_PropertyPath);
        }
    }
}