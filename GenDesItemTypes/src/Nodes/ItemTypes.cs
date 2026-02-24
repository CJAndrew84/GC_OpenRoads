using GenDes.ItemTypes.Extensions;
using Bentley.DgnPlatformNET;
using Bentley.DgnPlatformNET.DgnEC;
using Bentley.DgnPlatformNET.Elements;
using Bentley.ECObjects.Instance;
using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.GeneralPurpose;
using Bentley.GenerativeComponents.ScriptEditor.Controls.ExpressionEditor;
//using Bentley.GenerativeComponents.GCScript.NameScopes;
//using Bentley.GenerativeComponents.GeneralPurpose;
//using Bentley.GenerativeComponents.MicroStation;
using Bentley.GenerativeComponents.View;
using Bentley.MstnPlatformNET;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GenDes.ItemTypes
{
    using EECC = ExpressionEditorCustomConfiguration;
    using EECCAttr = GCExpressionEditorCustomConfigurationAttribute;

    [GCNamespace("GC")]
    [GCNodeTypePaletteCategory("{Gen:Des} Add-in")]
    [GCNodeTypeIcon("Resources/ItemTypes.png")]
    [GCSummary("Read, attach and update item properties on elements")]
    [GCHideInheritedTechniques]
    public class GenDesItemTypes : GeometricNode
    {
        private static readonly DgnFile _activeDgnFile = Session.Instance.GetActiveDgnFile();
        private static ItemType _selectedItemType { get; set; }

        #region Supporting Methods
        static EECC GetEECCForItems()
        {
            return new EECC(getItemLibs, ScriptChoiceOptions.UserMayOverrideChoices);

            IEnumerable<ScriptChoice> getItemLibs(ExpressionEditor parentExpressionEditor)
            {
                GenDesItemTypes node = (GenDesItemTypes)parentExpressionEditor.MeaningOfThis;
                ScriptChoiceList result = node.GetItemLibList();
                return result;
            }
        }

        ScriptChoiceList GetItemLibList()
        {
            IList<ItemTypeLibrary> itemLibs = ItemTypeLibrary.PopulateListFromFile(_activeDgnFile);

            ScriptChoiceList libList = new ScriptChoiceList();
            for (int i = 0; i < itemLibs.Count; i++)
            {
                ScriptChoice itemList = new ScriptChoice(itemLibs[i].DisplayLabel);
                foreach (ItemType itemType in itemLibs[i].ItemTypes)
                {
                    itemList.AddSubChoice(itemType.ItemNamesConcat().ToQuotedScriptText());
                }
                libList.Add(itemList);
            }

            return libList;
        }

        public static bool CreateProperty(string item, ItemType itemType)
        {
            try
            {
                var PropName = item.Split(':')[0];
                var PropValue = item.Split(':')[1];
                var PropType = item.Split(':')[2];
                var PropExpression = item.Split(':')[3];

                CustomProperty ExportProp = itemType.AddProperty(PropName);
                ExportProp.DefaultValue = PropValue;
                CustomProperty.TypeKind typeKind;
                Enum.TryParse(PropType, out typeKind);
                ExportProp.Type = typeKind;
                ExportProp.SetExpression(PropExpression, "Error");
                return true;
            }
            catch { return false; }

        }

        public static string[] ConcatProperties(ItemType itemType)
        {
            List<string> propList = new List<string>();
            IEnumerator<CustomProperty> properties = itemType.GetEnumerator();
            while (properties.MoveNext())
            {
                CustomProperty prop = properties.Current;
                propList.Add(prop.DisplayLabel);
            }
            if (propList.Any())
                return propList.ToArray();
            else
                return null;
        }

        #endregion

        [GCDefaultTechnique]
        [GCSummary("Select an ItemType from the active design file to read property information")]
        //[GCParameter("ItemType", "The ItemType to read from (must be imported in the active design file)")]
        public NodeUpdateResult GetItemTypeInfo
        (
            NodeUpdateContext updateContext,
            [EECCAttr(nameof(GetEECCForItems))] string ItemType,
            [GCOut, GCInitiallyPinned] ref string ItemTypeName,
            [GCOut, GCInitiallyPinned] ref string[] ItemTypeProperties
        )
        {
            try
            {
                if (string.IsNullOrEmpty(ItemType))
                {
                    ItemTypeProperties.Clear();
                }
                else
                {
                    //Get item type
                    _selectedItemType = ItemType.GetItemTypeByConcatString();

                    //Add item properties to output
                    List<string> propList = new List<string>();
                    IEnumerator<CustomProperty> properties = _selectedItemType.GetEnumerator();
                    while (properties.MoveNext())
                    {
                        CustomProperty prop = properties.Current;
                        propList.Add(prop.DisplayLabel);
                    }
                    if (propList.Any())
                        ItemTypeProperties = propList.ToArray();
                    else
                        ItemTypeProperties = null;

                    //Pass selected item type to output
                    ItemTypeName = ItemType;
                }
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;
        }

        /*
        [GCTechnique]
        [GCSummary("Create Item Type Library")]
        public NodeUpdateResult CreateItemTypeLibrary
        (
            NodeUpdateContext updateContext,
            [GCIn, GCInitiallyPinned] string LibraryName,
            [GCOut, GCInitiallyPinned] ref string itemTypeLibrary
        )
        {
            try
            {
                ItemTypeLibrary library = ItemTypeLibrary.FindByName(LibraryName, Session.Instance.GetActiveDgnFile());

                if (library == null)
                {
                    library = ItemTypeLibrary.Create(LibraryName, Session.Instance.GetActiveDgnFile());

                    library.Write();

                    itemTypeLibrary = library.DisplayLabel;
                }
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;

        }

        [GCTechnique]
        [GCSummary("Create Item Type Library")]
        public NodeUpdateResult CreateItemTypeInLibrary
        (
            NodeUpdateContext updateContext,
            [GCIn, GCInitiallyPinned] string LibraryName,
            [GCIn, GCInitiallyPinned] string itemTypeName,
            [GCOut, GCInitiallyPinned] ref string ItemTypeName
        )
        {
            try
            {
                ItemTypeLibrary library = ItemTypeLibrary.FindByName(LibraryName, Session.Instance.GetActiveDgnFile());

                if (library == null)
                {
                    library = ItemTypeLibrary.Create(LibraryName, Session.Instance.GetActiveDgnFile());

                    ItemType itemType = library.AddItemType(itemTypeName);

                    library.Write();

                    ItemTypeName = itemType.ItemNamesConcat().ToQuotedScriptText();
                }
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;

        }

        [GCTechnique]
        [GCSummary("Create Property string")]
        public NodeUpdateResult CreatePropertyString
        (
            NodeUpdateContext updateContext,
            [GCIn, GCInitiallyPinned] string PropName,
            [GCIn, GCInitiallyPinned] string PropValue,
            [GCIn, GCInitiallyPinned] string PropType,
            [GCIn, GCInitiallyPinned] string PropExpression,
            [GCOut, GCInitiallyPinned] ref string PropertyString
        )
        {
            try
            {
                PropertyString = $"{PropName}:{PropValue}:{PropType}:{PropExpression}";
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;
        }

        [GCTechnique]
        [GCSummary("Create Item Type Library")]
        [GCParameter("Properties", "Array of new properties, must be enclosed by curly brackets ({}). Name, Default Value,Type and Expression to be seperated by :. Property Type Options are: Boolean,DateTime,Double,Integer,Point,String,Custom. Example: Name:Value:Type:Expression")]
        public NodeUpdateResult CreateItemTypeProperties
        (
            NodeUpdateContext updateContext,
            [GCIn, GCInitiallyPinned] string LibraryName,
            [GCIn, GCInitiallyPinned] string itemTypeName,
            [GCIn, GCInitiallyPinned] string[] Properties,
            [GCOut, GCInitiallyPinned] ref string ItemTypeName,
            [GCOut, GCInitiallyPinned] ref string[] ItemTypeProperties
        )
        {
            try
            {
                ItemTypeLibrary library = ItemTypeLibrary.FindByName(LibraryName, Session.Instance.GetActiveDgnFile());

                if (library == null)
                {
                    library = ItemTypeLibrary.Create(LibraryName, Session.Instance.GetActiveDgnFile());

                    ItemType itemType = library.AddItemType(itemTypeName);

                    foreach (var item in Properties)
                    {
                        var created = CreateProperty(item, itemType);
                    }

                    library.Write();

                    ItemTypeName = itemType.ItemNamesConcat().ToQuotedScriptText();

                    ItemTypeProperties = ConcatProperties(itemType);
                }
                else
                {
                    if (library.GetItemTypeByName(itemTypeName) == null)
                    {
                        ItemType itemType = library.AddItemType(itemTypeName);

                        foreach (var item in Properties)
                        {
                            var created = CreateProperty(item, itemType);
                        }

                        library.Write();

                        ItemTypeName = itemType.ItemNamesConcat().ToQuotedScriptText();

                        ItemTypeProperties = ConcatProperties(itemType);
                    }
                    else
                    {
                        ItemType itemType = library.GetItemTypeByName(itemTypeName);

                        if (itemType.GetPropertyByName(Properties[0].Split(':')[0]) == null)
                        {
                            foreach (var item in Properties)
                            {
                                var created = CreateProperty(item, itemType);
                            }

                            library.Write();

                            ItemTypeName = itemType.ItemNamesConcat().ToQuotedScriptText();

                            ItemTypeProperties = ConcatProperties(itemType);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;

        }
        */
        [GCTechnique]
        [GCSummary("Read the Item Type properties attached to elements")]
        [GCParameter("ElementsToRead", "Input elements to read custom item type data from")]
        [GCParameter("ItemTypeName", "The concatenated library and item type name")]
        public NodeUpdateResult ReadItems
        (
            NodeUpdateContext updateContext,
            [GCDgnModelProvider, GCReplicatable] GeometricNode ElementsToRead,
            [GCIn] string ItemTypeName,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref IGCObject[] ItemProperties
        )
        {
            if (string.IsNullOrEmpty(ItemTypeName))
                return new NodeUpdateResult.IncompleteInputs(ItemTypeName);

            Boxer boxer = GCEnvironment().Boxer;

            try
            {
                //Update ItemType selection if required
                if (this.ReplicationIndex < 1)
                {
                    //Clear previous values
                    this.ClearReplicatedChildNodes();

                    //Update selected item if not matching input
                    if (_selectedItemType == null || _selectedItemType.ItemNamesConcat() != ItemTypeName)
                        _selectedItemType = ItemTypeName.GetItemTypeByConcatString();
                }

                //Get platformNET element
                long id = ElementsToRead.GCElement().ElementId();
                Element element = ElementsToRead.DgnModel().FindElementById(new ElementId(ref id));

                //Get custom item properties
                CustomItemHost customItemHost = new CustomItemHost(element, false);
                IDgnECInstance ecInstance = customItemHost.GetCustomItem(_selectedItemType.Library.DisplayLabel, _selectedItemType.DisplayLabel);
                if (ecInstance == null)
                    throw new Exception($"Item not found on element");

                List<IGCObject> values = new List<IGCObject>();
                IEnumerator<IECPropertyValue> ie = ecInstance.GetEnumerator(true, true);
                while (ie.MoveNext())
                {
                    if (ie.Current.IsNull)
                        continue;

                    //values.Add(ie.Current.NativeValue);
                    if (boxer.TryBox(out IGCObject result, ie.Current.NativeValue))
                        values.Add(result);
                    else
                        throw new Exception($"Error boxing value '{ie.Current.NativeValue}' for item property '{ie.Current.AccessString}'");
                }

                ItemProperties = values.ToArray();
                //this.AddOrSetOutputOnlyAdHocProperty("props", values, isInitiallyPinned: true, description: Ls.Literal("The item type property values"));

            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;
        }

        [GCTechnique]
        [GCSummary("Attach an ItemTypeLibrary and/or write new values to Items on the input elements")]
        [GCParameter("ElementsToWriteTo", "Input elements to write the custom item type data to")]
        [GCParameter("ItemTypeName", "The concatenated library and item type name")]
        public NodeUpdateResult WriteItems
        (
            NodeUpdateContext updateContext,
            [GCDgnModelProvider, GCReplicatable] GeometricNode ElementsToWriteTo,
            [GCIn] string ItemTypeName,
            [GCIn] IGCObject[] Properties,
            [GCIn] IGCObject[] Values
        )
        {
            try
            {
                //Update ItemType selection if required
                if (this.ReplicationIndex < 1)
                {
                    //Update selected item if not matching input
                    if (_selectedItemType == null || _selectedItemType.ItemNamesConcat() != ItemTypeName)
                        _selectedItemType = ItemTypeName.GetItemTypeByConcatString();
                }

                if (ItemTypeName.ItemNamesSplitLib(out string libName, out string itemName))
                {
                    DgnModel dgnModel = Session.Instance.GetActiveDgnModel();

                    long id = ElementsToWriteTo.GCElement().ElementId();
                    Element element = dgnModel.FindElementById(new ElementId(ref id)).Clone();
                    this.SetElement(element);

                    CustomItemHost customItemHost = new CustomItemHost(element, false);

                    IDgnECInstance ecInstance = customItemHost.GetCustomItem(libName, itemName);
                    if (ecInstance == null)
                    {
                        ecInstance = customItemHost.ApplyCustomItem(_selectedItemType);
                    }
                    for (int i = 0; i < Properties.Length; i++)
                    {
                        string propName = Properties[i].Unbox<string>();
                        object propValue = Values[i].Unbox<object>();
                        //bool getName = Bentley.ECObjects.Schema.ECNameValidation.EncodeToValidName(ref propName);
                        ecInstance.SetAsString(propName, propValue.ToString());
                    }

                    ecInstance.WriteChanges();

                }
                else
                    return new NodeUpdateResult.TechniqueInvalidArguments(ItemTypeName);
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        [GCTechnique]
        [GCSummary("Attach an ItemTypeLibrary and/or write new values to Items on the input elements")]
        [GCParameter("ElementsToWriteTo", "Input elements to write the custom item type data to")]
        [GCParameter("ItemTypeName", "The concatenated library and item type name")]
        public NodeUpdateResult WriteItemsMultipleValues
        (
            NodeUpdateContext updateContext,
            [GCDgnModelProvider, GCReplicatable] GeometricNode ElementsToWriteTo,
            [GCIn] string ItemTypeName,
            [GCIn] IGCObject[] Properties,
            [GCIn] IGCObject[] Values
        )
        {
            try
            {
                //Update ItemType selection if required
                if (this.ReplicationIndex < 1)
                {
                    //Update selected item if not matching input
                    if (_selectedItemType == null || _selectedItemType.ItemNamesConcat() != ItemTypeName)
                        _selectedItemType = ItemTypeName.GetItemTypeByConcatString();
                }

                int obj = this.ReplicationIndex;

                if (ItemTypeName.ItemNamesSplitLib(out string libName, out string itemName))
                {
                    DgnModel dgnModel = Session.Instance.GetActiveDgnModel();

                    long id = ElementsToWriteTo.GCElement().ElementId();
                    Element element = dgnModel.FindElementById(new ElementId(ref id)).Clone();
                    this.SetElement(element);

                    CustomItemHost customItemHost = new CustomItemHost(element, false);

                    IDgnECInstance ecInstance = customItemHost.GetCustomItem(libName, itemName);
                    if (ecInstance == null)
                    {
                        ecInstance = customItemHost.ApplyCustomItem(_selectedItemType);
                    }
                    for (int i = 0; i < Properties.Length; i++)
                    {
                        string propName = Properties[i].Unbox<string>();
                        object[] propGroup = Values[i].Unbox<object[]>();
                        string propValue = propGroup[obj].ToString();
                        //string propValue = Values.ToList()[i][obj].ToString();
                        //bool getName = Bentley.ECObjects.Schema.ECNameValidation.EncodeToValidName(ref propName);
                        ecInstance.SetAsString(propName, propValue);
                    }

                    ecInstance.WriteChanges();
                }
                else
                    return new NodeUpdateResult.TechniqueInvalidArguments(ItemTypeName);
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        [GCTechnique]
        [GCSummary("Attach an ItemTypeLibrary and/or write a Counter to Items on the input elements")]
        [GCParameter("ElementsToWriteTo", "Input elements to write the custom item type data to")]
        [GCParameter("ItemTypeName", "The concatenated library and item type name")]
        public NodeUpdateResult WriteCounterItems
        (
            NodeUpdateContext updateContext,
            [GCDgnModelProvider, GCReplicatable] GeometricNode ElementsToWriteTo,
            [GCIn] string ItemTypeName,
            [GCIn] string prefix,
            [GCIn] IGCObject CounterProperty,
            [GCIn] double CounterStart,
            [GCIn] string suffix
        )
        {
            try
            {
                //Update ItemType selection if required
                if (this.ReplicationIndex < 1)
                {
                    //Update selected item if not matching input
                    if (_selectedItemType == null || _selectedItemType.ItemNamesConcat() != ItemTypeName)
                        _selectedItemType = ItemTypeName.GetItemTypeByConcatString();
                }

                int obj = this.ReplicationIndex;

                if (ItemTypeName.ItemNamesSplitLib(out string libName, out string itemName))
                {
                    DgnModel dgnModel = Session.Instance.GetActiveDgnModel();

                    long id = ElementsToWriteTo.GCElement().ElementId();
                    Element element = dgnModel.FindElementById(new ElementId(ref id)).Clone();
                    this.SetElement(element);

                    CustomItemHost customItemHost = new CustomItemHost(element, false);

                    IDgnECInstance ecInstance = customItemHost.GetCustomItem(libName, itemName);
                    if (ecInstance == null)
                    {
                        ecInstance = customItemHost.ApplyCustomItem(_selectedItemType);
                    }
                    string propName = CounterProperty.Unbox<string>();

                    string propValue = prefix + (obj + CounterStart).ToString("0000") + suffix;
                    //string propValue = Values.ToList()[i][obj].ToString();
                    //bool getName = Bentley.ECObjects.Schema.ECNameValidation.EncodeToValidName(ref propName);
                    ecInstance.SetAsString(propName, propValue);


                    ecInstance.WriteChanges();
                }
                else
                    return new NodeUpdateResult.TechniqueInvalidArguments(ItemTypeName);
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        [GCTechnique]
        [GCSummary("Attach an ItemTypeLibrary to the input elements")]
        [GCParameter("ElementsToWriteTo", "Input elements to write the custom item type data to")]
        [GCParameter("ItemTypeName", "The concatenated library and item type name")]
        public NodeUpdateResult AttachItems
        (
            NodeUpdateContext updateContext,
            [GCDgnModelProvider, GCReplicatable] GeometricNode ElementsToWriteTo,
            [GCIn] string ItemTypeName
        )
        {
            try
            {
                //Update ItemType selection if required
                if (this.ReplicationIndex < 1)
                {
                    //Update selected item if not matching input
                    if (_selectedItemType == null || _selectedItemType.ItemNamesConcat() != ItemTypeName)
                        _selectedItemType = ItemTypeName.GetItemTypeByConcatString();
                }

                if (ItemTypeName.ItemNamesSplitLib(out string libName, out string itemName))
                {
                    DgnModel dgnModel = Session.Instance.GetActiveDgnModel();

                    long id = ElementsToWriteTo.GCElement().ElementId();
                    Element element = dgnModel.FindElementById(new ElementId(ref id)).Clone();
                    this.SetElement(element);

                    CustomItemHost customItemHost = new CustomItemHost(element, false);

                    IDgnECInstance ecInstance = customItemHost.GetCustomItem(libName, itemName);
                    if (ecInstance == null)
                    {
                        ecInstance = customItemHost.ApplyCustomItem(_selectedItemType);
                    }

                    ecInstance.WriteChanges();
                }
                else
                    return new NodeUpdateResult.TechniqueInvalidArguments(ItemTypeName);
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        [GCTechnique]
        [GCSummary("Attach an ItemTypeLibrary to the input elements")]
        [GCParameter("ElementsToWriteTo", "Input elements to write the custom item type data to")]
        [GCParameter("ItemTypeName", "The concatenated library and item type name")]
        public NodeUpdateResult AttachMultipleItems
        (
            NodeUpdateContext updateContext,
            [GCDgnModelProvider, GCReplicatable] GeometricNode ElementsToWriteTo,
            [GCIn] string[] ItemTypeNames
        )
        {
            try
            {
                foreach (var ItemTypeName in ItemTypeNames)
                {
                    //Update ItemType selection if required
                    if (this.ReplicationIndex < 1)
                    {
                        //Update selected item if not matching input
                        if (_selectedItemType == null || _selectedItemType.ItemNamesConcat() != ItemTypeName)
                            _selectedItemType = ItemTypeName.GetItemTypeByConcatString();
                    }

                    if (ItemTypeName.ItemNamesSplitLib(out string libName, out string itemName))
                    {
                        DgnModel dgnModel = Session.Instance.GetActiveDgnModel();

                        long id = ElementsToWriteTo.GCElement().ElementId();
                        Element element = dgnModel.FindElementById(new ElementId(ref id)).Clone();
                        this.SetElement(element);

                        CustomItemHost customItemHost = new CustomItemHost(element, false);

                        IDgnECInstance ecInstance = customItemHost.GetCustomItem(libName, itemName);
                        if (ecInstance == null)
                        {
                            ecInstance = customItemHost.ApplyCustomItem(_selectedItemType);
                        }

                        ecInstance.WriteChanges();
                    }
                    else
                        return new NodeUpdateResult.TechniqueInvalidArguments(ItemTypeName);
                }
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }
        [GCTechnique]
        [GCSummary("Attach and Write data to the M80 RR NEL_Data Item Type")]
        [GCParameter("ElementsToWriteTo", "Input elements to write the custom item type data to")]
        [GCParameter("ItemTypeName", "The concatenated library and item type name")]
        [GCParameter("Direction", "PD or CD")]
        [GCParameter("ControlStringName", "Example: MBA1")]
        [GCParameter("Asset_Type", "Example: BATB")]
        [GCParameter("Asset_Sub_Type", "Example: KERB")]
        [GCParameter("Additional_Asset_Type_Id", "Example: A, B, C etc or 0")]
        [GCParameter("Unique_Asset_Code", "Example: TB21")]
        [GCParameter("CounterStart", "Example: 1")]
        [GCParameter("Side_Control", "Example: L or R")]
        [GCParameter("NEL_PDS_Code", "Example: N")]
        [GCParameter("NEL_PDS_Phase", "Example: D")]
        [GCParameter("NEL_PDS_Subphase", "Example: TOC")]
        [GCParameter("NEL_GUID", "Example: NA")]
        [GCParameter("NEL_Uniclass_SS_Code", "Example: Ss_25_16_94_50")]
        [GCParameter("NEL_Uniclass_SS_Description", "Example: Metal vehicle safety fence systems")]
        [GCParameter("NEL_Uniclass_PR_Code", "Example: Pr_20_85_07_75")]
        [GCParameter("NEL_Uniclass_PR_Description", "Example: Safety barrier terminals")]
        [GCParameter("NEL_Volume", "Example: 0.25")]
        [GCParameter("NEL_Material_Grouping", "Example: Steel")]
        [GCParameter("NEL_Grade", "Example: NA")]
        [GCParameter("NEL_MEA_Disc_L2", "Example: BR04")]
        [GCParameter("NEL_File_Name", "Example: NEL-NTH-NNA-3100-CRW-MD3-0001")]
        [GCParameter("NEL_Length", "Example: 0.083")]
        [GCParameter("NEL_Width", "Example: 0.083")]
        [GCParameter("NEL_Height", "Example: 0.083")]
        [GCParameter("NEL_Depth", "Example: 0.083")]
        [GCParameter("NEL_PDS_DesignPackage", "Example: 3100-CBR-DPK-3102")]
        [GCParameter("NEL_Room_Name", "Example: NA")]
        [GCParameter("NEL_Level_Name", "Example: NA")]
        [GCParameter("NEL_PDS_WorkArea", "Example: TBC")]
        [GCParameter("NEL_Construction_Staging", "Example: New")]
        [GCParameter("NEL_ReturnedAssetOwnerAssetID", "Example: TBC")]
        [GCParameter("NEL_Asset_Owner_Maintainer", "Example: TBC")]
        [GCParameter("NEL_Asset_Owner", "Example: TBC")]
        [GCParameter("NEL_Object_Size", "Example: NA")]
        [GCParameter("NEL_Gradient", "Example: NA")]
        [GCParameter("NEL_Invert_US", "Example: NA")]
        [GCParameter("NEL_Invert_DS", "Example: NA")]
        [GCParameter("NEL_Diameter", "Example: NA")]
        [GCParameter("NEL_Area", "Example: NA")]
        [GCParameter("NEL_Room_No", "Example: NA")]
        public NodeUpdateResult NELDataItemType
        (
            NodeUpdateContext updateContext,
            [GCDgnModelProvider, GCReplicatable] GeometricNode ElementsToWriteTo,
            [GCIn, GCInitialValue("North East Link - North (Barriers_GC) :: NEL_DATA")] string ItemTypeName,
            [GCIn, GCInitialValue("PD")] string Direction,
            [GCIn, GCInitialValue("MBT1")] string ControlStringName,
            [GCIn, GCInitialValue("BATB")] string Asset_Type,
            [GCIn, GCInitialValue("XXXX")] string Asset_Sub_Type,
            [GCIn, GCInitialValue("0")] string Additional_Asset_Type_Id,
            [GCIn, GCInitialValue("TB21")] string Unique_Asset_Code,
            [GCIn, GCInitialValue(1)] double CounterStart,
            [GCIn, GCInitialValue("L")] string Side_Control,
            [GCIn, GCInitialValue("N")] string NEL_PDS_Code,
            [GCIn, GCInitialValue("D")] string NEL_PDS_Phase,
            [GCIn, GCInitialValue("TOC")] string NEL_PDS_Subphase,
            [GCOptional, GCInitialValue("NA")] string NEL_GUID,
            [GCIn] string NEL_Uniclass_SS_Code,
            [GCIn] string NEL_Uniclass_SS_Description,
            [GCIn] string NEL_Uniclass_PR_Code,
            [GCIn] string NEL_Uniclass_PR_Description,
            [GCOptional, GCInitialValue("NA")] string NEL_Volume,
            [GCIn] string NEL_Material_Grouping,
            [GCIn] string NEL_Grade,
            [GCIn, GCInitialValue("BR04")] string NEL_MEA_Disc_L2,
            [GCIn, GCInitialValue("NEL-NTH-NNA-3100-CRW-MD3-0001")] string NEL_File_Name,
            [GCIn] string NEL_Length,
            [GCIn] string NEL_Width,
            [GCIn] string NEL_Height,
            [GCIn] string NEL_Depth,
            [GCIn, GCInitialValue("3100-CBR-DPK-3102")] string NEL_PDS_DesignPackage,
            [GCOptional, GCInitialValue("NA")] string NEL_Room_Name,
            [GCOptional, GCInitialValue("NA")] string NEL_Level_Name,
            [GCIn, GCInitialValue("TBC")] string NEL_PDS_WorkArea,
            [GCIn, GCInitialValue("New")] string NEL_Construction_Staging,
            [GCOptional, GCInitialValue("TBC")] string NEL_ReturnedAssetOwnerAssetID,
            [GCOptional, GCInitialValue("TBC")] string NEL_Asset_Owner_Maintainer,
            [GCOptional, GCInitialValue("TBC")] string NEL_Asset_Owner,
            [GCOptional, GCInitialValue("NA")] string NEL_Object_Size,
            [GCIn] string NEL_Material_Name,
            [GCOptional, GCInitialValue("NA")] string NEL_Gradient,
            [GCOptional, GCInitialValue("NA")] string NEL_Invert_US,
            [GCOptional, GCInitialValue("NA")] string NEL_Invert_DS,
            [GCOptional, GCInitialValue("NA")] string NEL_Diameter,
            [GCOptional, GCInitialValue("NA")] string NEL_Area,
            [GCOptional, GCInitialValue("NA")] string NEL_Room_No
        )
        {
            try
            {
                //Update ItemType selection if required
                if (this.ReplicationIndex < 1)
                {
                    //Update selected item if not matching input
                    if (_selectedItemType == null || _selectedItemType.ItemNamesConcat() != ItemTypeName)
                        _selectedItemType = ItemTypeName.GetItemTypeByConcatString();
                }

                int obj = this.ReplicationIndex;

                string ctl = ControlStringName.Substring(1, 3);

                string prefix = Direction + '-' + ctl + '-' + NEL_MEA_Disc_L2 + '-' + Asset_Type + '-' + Asset_Sub_Type + '-' + Additional_Asset_Type_Id + '-' + Unique_Asset_Code + '-';

                string suffix = '-' + Side_Control;

                string filename = Session.Instance.GetActiveDgnFile().GetFileName();
                string filenameNoExt = filename.Split(new string[] { ".dgn" }, StringSplitOptions.None).First().Split(new string[] { "\\" }, StringSplitOptions.None).Last();

                string NEL_PDS_Zone = filenameNoExt.Split('-')[3];
                string NEL_PDS_SubZone = NEL_PDS_Zone.Substring(0, 2) + "00";
                string NEL_PDS_Discipline = filenameNoExt.Split('-')[4];

                string NEL_Object_Type = Unique_Asset_Code;

                string NEL_WBS_P6_Code = NEL_PDS_Code + '-' + NEL_PDS_Phase + '-' + NEL_PDS_SubZone + '-' + NEL_PDS_Discipline + '-' + NEL_MEA_Disc_L2;

                if (ItemTypeName.ItemNamesSplitLib(out string libName, out string itemName))
                {
                    DgnModel dgnModel = Session.Instance.GetActiveDgnModel();

                    long id = ElementsToWriteTo.GCElement().ElementId();
                    Element element = dgnModel.FindElementById(new ElementId(ref id)).Clone();
                    this.SetElement(element);

                    CustomItemHost customItemHost = new CustomItemHost(element, false);

                    IDgnECInstance ecInstance = customItemHost.GetCustomItem(libName, itemName);
                    if (ecInstance == null)
                    {
                        ecInstance = customItemHost.ApplyCustomItem(_selectedItemType);
                    }

                    string NEL_Object_Name = prefix + (obj + CounterStart).ToString("0000") + suffix;

                    string NEL_Unique_Asset_Identifier = NEL_PDS_SubZone + '-' + NEL_MEA_Disc_L2 + '-' + NEL_Object_Type + '-' + NEL_Object_Name;

                    string NEL_Element_ID = NEL_PDS_SubZone + '-' + NEL_Object_Type + '-' + NEL_Object_Name;

                    ecInstance.SetAsString("NEL_Object_Name", NEL_Object_Name);
                    ecInstance.SetAsString("NEL_PDS_Zone", NEL_PDS_Zone);
                    ecInstance.SetAsString("NEL_PDS_SubZone", NEL_PDS_SubZone);
                    ecInstance.SetAsString("NEL_PDS_Code", NEL_PDS_Code);
                    ecInstance.SetAsString("NEL_PDS_Phase", NEL_PDS_Phase);
                    ecInstance.SetAsString("NEL_PDS_Subphase", NEL_PDS_Subphase);
                    ecInstance.SetAsString("NEL_PDS_Discipline", NEL_PDS_Discipline);
                    ecInstance.SetAsString("NEL_GUID", NEL_GUID);
                    ecInstance.SetAsString("NEL_Uniclass_SS_Code", NEL_Uniclass_SS_Code);
                    ecInstance.SetAsString("NEL_Uniclass_SS_Description", NEL_Uniclass_SS_Description);
                    ecInstance.SetAsString("NEL_Uniclass_PR_Code", NEL_Uniclass_PR_Code);
                    ecInstance.SetAsString("NEL_Uniclass_PR_Description", NEL_Uniclass_PR_Description);
                    ecInstance.SetAsString("NEL_Volume", NEL_Volume);
                    ecInstance.SetAsString("NEL_Material_Grouping", NEL_Material_Grouping);
                    ecInstance.SetAsString("NEL_Grade", NEL_Grade);
                    ecInstance.SetAsString("NEL_MEA_High_Disc_L1", NEL_PDS_Discipline);
                    ecInstance.SetAsString("NEL_MEA_Disc_L2", NEL_MEA_Disc_L2);
                    ecInstance.SetAsString("NEL_WBS_P6_Code", NEL_WBS_P6_Code);
                    ecInstance.SetAsString("NEL_File_Name", NEL_File_Name);
                    ecInstance.SetAsString("NEL_Length", NEL_Length);
                    ecInstance.SetAsString("NEL_Width", NEL_Width);
                    ecInstance.SetAsString("NEL_Height", NEL_Height);
                    ecInstance.SetAsString("NEL_Depth", NEL_Depth);
                    ecInstance.SetAsString("NEL_PDS_DesignPackage", NEL_PDS_DesignPackage);
                    ecInstance.SetAsString("NEL_Room_Name", NEL_Room_Name);
                    ecInstance.SetAsString("NEL_Level_Name", NEL_Level_Name);
                    ecInstance.SetAsString("NEL_PDS_WorkArea", NEL_PDS_WorkArea);
                    ecInstance.SetAsString("NEL_PDS_SubZone", NEL_PDS_SubZone);
                    ecInstance.SetAsString("NEL_Object_Type", NEL_Object_Type);
                    ecInstance.SetAsString("NEL_Unique_Asset_Identifier", NEL_Unique_Asset_Identifier);
                    ecInstance.SetAsString("NEL_Construction_Staging", NEL_Construction_Staging);
                    ecInstance.SetAsString("NEL_Element_ID", NEL_Element_ID);
                    ecInstance.SetAsString("NEL_ReturnedAssetOwnerAssetID", NEL_ReturnedAssetOwnerAssetID);
                    ecInstance.SetAsString("NEL_Asset_Owner_Maintainer", NEL_Asset_Owner_Maintainer);
                    ecInstance.SetAsString("NEL_Asset_Owner", NEL_Asset_Owner);
                    ecInstance.SetAsString("NEL_Object_Size", NEL_Object_Size);
                    ecInstance.SetAsString("NEL_Material_Name", NEL_Material_Name);
                    ecInstance.SetAsString("NEL_Gradient", NEL_Gradient);
                    ecInstance.SetAsString("NEL_Invert_US", NEL_Invert_US);
                    ecInstance.SetAsString("NEL_Invert_DS", NEL_Invert_DS);
                    ecInstance.SetAsString("NEL_Diameter", NEL_Diameter);
                    ecInstance.SetAsString("NEL_Area", NEL_Area);
                    ecInstance.SetAsString("NEL_Room_No", NEL_Room_No);

                    ecInstance.WriteChanges();
                }
                else
                    return new NodeUpdateResult.TechniqueInvalidArguments(ItemTypeName);
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }
    }
}
