﻿//===============================================================================
// Microsoft patterns & practices Enterprise Library
// Core
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Configuration;
using System.ComponentModel;
using Microsoft.Practices.EnterpriseLibrary.Configuration.Design.ViewModel.Services;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Common.Utility;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration.Design;
using Microsoft.Practices.EnterpriseLibrary.Configuration.Design.Properties;
using Microsoft.Practices.Unity;
using System.Diagnostics;

namespace Microsoft.Practices.EnterpriseLibrary.Configuration.Design.ViewModel
{
    public class ElementCollectionViewModel : ElementViewModel
    {
        readonly ConfigurationElementCollection thisElementCollection;
        readonly ConfigurationCollectionAttribute configurationCollectionAttribute;
        readonly ConfigurationPropertyAttribute configurationPropertyAttribute;
        readonly IMergeableConfigurationElementCollection mergeableConfigurationCollection;

        private Type[] polymorphicCollectionElementTypes;
        private Type customPolyporpicCollectionElementType;
        
        DiscoverDerivedConfigurationTypesService configurationTypesService;

        [InjectionConstructor]
        public ElementCollectionViewModel(ElementViewModel parentElementModel, PropertyDescriptor declaringProperty)
            : base(parentElementModel, declaringProperty)
        {
            this.thisElementCollection = declaringProperty.GetValue(parentElementModel.ConfigurationElement) as ConfigurationElementCollection;
            this.IsPolymorphicCollection = ConfigurationType.FindGenericParent(typeof(PolymorphicConfigurationElementCollection<>)) != null;
            this.mergeableConfigurationCollection = MergeableConfigurationCollectionFactory.GetCreateMergeableCollection(thisElementCollection);

            Type polymorphicCollectionWithCustomElementType = ConfigurationType.FindGenericParent(typeof(NameTypeConfigurationElementCollection<,>));
            if (polymorphicCollectionWithCustomElementType != null)
            {
                customPolyporpicCollectionElementType = polymorphicCollectionWithCustomElementType.GetGenericArguments()[1];
            }

            configurationPropertyAttribute = base.Attributes.OfType<ConfigurationPropertyAttribute>().FirstOrDefault();
            Debug.Assert(configurationPropertyAttribute != null);

            configurationCollectionAttribute = base.Attributes.OfType<ConfigurationCollectionAttribute>().FirstOrDefault();
            Debug.Assert(configurationCollectionAttribute != null);
        }

        [InjectionMethod]
        public void ElementCollectionViewModelServiceDependencies(DiscoverDerivedConfigurationTypesService configurationTypesService)
        {
            this.configurationTypesService = configurationTypesService;
        }

        protected override IEnumerable<CommandModel> GetAllCommands()
        {
            var baseCommands = base.CreateCustomCommands()
                                    .Union(GetPromotedCommands());

            return baseCommands.Union(new CommandModel[]{ContainingSection.CreateElementCollectionAddCommands(Attributes, this)});
        }

        public Type CollectionElementType
        {
            get
            {
                return configurationCollectionAttribute.ItemType;
            }
        }

        public bool IsPolymorphicCollection
        {
            get; 
            private set;
        }

        public virtual Type[] PolymorphicCollectionElementTypes
        {
            get
            {
                if (polymorphicCollectionElementTypes == null)
                {
                    var availablePolymorphicTypes = configurationTypesService.FindAvailableConfigurationElementTypes(CollectionElementType);
                    if (customPolyporpicCollectionElementType != null) availablePolymorphicTypes = availablePolymorphicTypes.Union(new[] { customPolyporpicCollectionElementType });

                    polymorphicCollectionElementTypes = availablePolymorphicTypes
                            .Select( x=> new { ElementType = x, Browsable = TypeDescriptor.GetAttributes(x).OfType<BrowsableAttribute>().FirstOrDefault() })
                            .Where( x=>x.Browsable == null || x.Browsable.Browsable)
                            .Select( x=>x.ElementType)
                            .ToArray();
                }
                return polymorphicCollectionElementTypes;
            }
        }

        protected override string GetLocalPathPart()
        {
            if (configurationPropertyAttribute.IsDefaultCollection) return "";

            return configurationPropertyAttribute.Name;
        }

        public bool IsFirst(CollectionElementViewModel element)
        {
            if (!ChildElements.Any()) return false;

            return ChildElements.First().Path == element.Path;
        }

        public bool IsLast(CollectionElementViewModel element)
        {
            if (!ChildElements.Any()) return false;

            return ChildElements.Last().Path == element.Path;
        }

        protected override IEnumerable<ElementViewModel> GetAllChildElements()
        {
            var leaf = base.GetAllChildElements();

            var contained = thisElementCollection
                                .OfType<ConfigurationElement>()
                                .Select(x => new { Browasble = TypeDescriptor.GetAttributes(x).OfType<BrowsableAttribute>().FirstOrDefault(), Instance = x })
                                .Where(x => x.Browasble == null || x.Browasble.Browsable)
                                .Select(x => ContainingSection.CreateCollectionElement(this, x.Instance))
                                .Cast<ElementViewModel>();

            return leaf.Union(contained);
        }
        
        public virtual ElementViewModel AddNewCollectionElement(Type elementType)
        {
            var element = mergeableConfigurationCollection.CreateNewElement(elementType);
            var childElementModel = ContainingSection.CreateCollectionElement(this, element);

            if (childElementModel.NameProperty != null)
            {
                childElementModel.NameProperty.Value = CalculateNameFromType(elementType);
            }

            // add the new element to the configuration.
            mergeableConfigurationCollection.ResetCollection(
                thisElementCollection.OfType<ConfigurationElement>()
                .Concat(new [] { element })
                .ToArray());


            foreach (var property in childElementModel.Properties)
            {
                DesigntimeDefaultAttribute defaultDesigntimeValue = property.Attributes.OfType<DesigntimeDefaultAttribute>().FirstOrDefault();
                if (defaultDesigntimeValue != null)
                {
                    property.Value = property.ConvertFromBindableValue(defaultDesigntimeValue.DefaultValue);
                }
            }

            InitializeElementProperties(childElementModel);

            // add the new element to the view model.
            ChildElements.Add(childElementModel);


            Validate();

            return childElementModel;
        }

        private void ValidateDefiningProperty()
        {
            if (ParentElement == null) return;

            var declaringPropertyModel = ParentElement.Properties
                .Where(p => DeclaringProperty.Equals(p.DeclaringProperty)).FirstOrDefault();
            if (declaringPropertyModel == null) return;

            declaringPropertyModel.Validate();
        }

        private static void InitializeElementProperties(ElementViewModel childElementModel)
        {
            var propertiesForInitialization = childElementModel.Properties.OfType<INeedInitialization>();
            foreach(var propInitializer in propertiesForInitialization)
            {
                propInitializer.Initialize(new InitializeContext());
            }
        }

        //todo: should we have a Delete() on ElementViewModel too? then override on CollectionElement.
        // from here just call element.Delete();
        public void Delete(CollectionElementViewModel element)
        {
            //remove the element from configuration collection.
            var list =
                thisElementCollection.OfType<ConfigurationElement>().Where(x => x != element.ConfigurationElement /*todo: should compare on key-values, not on reference*/).
                    ToArray();
            mergeableConfigurationCollection.ResetCollection(list);

            //remove the element from the view.
            ChildElements.Remove(element);

            Validate();

            //notify deleted.
            element.OnDeleted();
        }

        #region calculate name methods todo: Externalize
        
        private string CalculateNameFromType(Type elementType)
        {
            var displayNameAttribute =
                TypeDescriptor.GetAttributes(elementType).OfType<DisplayNameAttribute>().FirstOrDefault();

            string baseName = displayNameAttribute == null
                                  ? TypeDescriptor.GetClassName(elementType)
                                  : displayNameAttribute.DisplayName;

            return FindUniqueNewName(baseName);
        }

        public string FindUniqueNewName(string baseName)
        {
            int number = 1;
            while(true)
            {
                string proposedName = string.Format(CultureInfo.CurrentUICulture,
                                                    Resources.NewCollectionElementNameFormat,
                                                    baseName,
                                                    number == 1 ? string.Empty : number.ToString()).Trim();

                if (this.ChildElements.Any(x => x.NameProperty != null && x.NameProperty.BindableProperty.BindableValue == proposedName))
                    number++;
                else
                    return proposedName;
            }
        }

        #endregion

        public override void Validate()
        {
            base.Validate();
            ValidateDefiningProperty();
        }

        #region Move Up & Down

        public void MoveUp(CollectionElementViewModel elementViewModel)
        {
           MoveElement(elementViewModel, -1);
        }

        public void MoveDown(CollectionElementViewModel elementViewModel)
        {
            MoveElement(elementViewModel, 1);         
        }

        private void MoveElement(CollectionElementViewModel element, int moveDistance)
        {
            var list = thisElementCollection.OfType<ConfigurationElement>().ToArray();
            
            //move the element in the configuration collection.
            MoveConfigurationItem(list, element.ConfigurationElement, moveDistance);
            mergeableConfigurationCollection.ResetCollection(list);

            //move the element in the view.
            MoveConfigurationItem(ChildElements, element, moveDistance);
        }

        private void MoveConfigurationItem<T>(IList<T> elements, T element, int relativeMoveIndex) where T : class
        {
            for(int i = 0; i < elements.Count(); i++)
            {
                if (elements[i] != element) continue;
                var newIndex = i + relativeMoveIndex;
                if (newIndex >= 0 && newIndex < elements.Count())
                {
                    var tmp = elements[newIndex];
                    elements[newIndex] = element;
                    elements[i] = tmp;
                    return;
                }
            }
        }
        
        #endregion
    }
}
