using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.GCScript.GCTypes;
using Bentley.GenerativeComponents.GCScript.ReflectedNativeTypeSupport;
using Bentley.GenerativeComponents.GeneralPurpose;
using Bentley.GenerativeComponents.UtilityNodes;
using Bentley.GenerativeComponents.View;
using System.Collections.Generic;
using System.Linq;

namespace GenDes.Lists
{
    [GCNamespace("List")]
    [GCNodeTypePaletteCategory("{Gen:Des} Lists")]
    [GCNodeTypeIcon("Resources/GroupBy.png")]
    [GCHideInheritedTechniques]
    public class GroupAndLookupByValue : UtilityNode
    {
        internal const string NameOfInputDataProperty = "InputData";
        internal const string NameOfGroupColumnIndexProperty = "GroupColumnIndex";
        internal const string NameOfGroupReturnColumn1IndexProperty = "GroupReturnColumn1Index";
        internal const string NameOfGroupReturnColumn2IndexProperty = "GroupReturnColumn2Index";
        internal const string NameOfGroupReturnColumn3IndexProperty = "GroupReturnColumn3Index";
        internal const string NameOfLookupColumnIndexProperty = "LookupColumnIndex";
        internal const string NameOfLookupReturnColumn1IndexProperty = "LookupReturnColumn1Index";
        internal const string NameOfLookupReturnColumn2IndexProperty = "LookupReturnColumn2Index";
        internal const string NameOfLookupReturnColumn3IndexProperty = "LookupReturnColumn3Index";
        internal const string NameOfResultProperty = "Result";

        static readonly NodeGCType s_gcTypeOfAllInstances = (NodeGCType)GCTypeTools.GetGCType(UniversalGCEnvironment.TheOnlyInstance, typeof(GroupAndLookupByValue));

        static public NodeGCType GCTypeOfAllInstances
        {
            get { return s_gcTypeOfAllInstances; }
        }

        static void AddAdditionalMembersToGCType(IGCEnvironment environment, GCType gcType, NativeNamespaceTranslator namespaceTranslator)
        {
            UtilityNodeTechnique technique1 = gcType.AddDefaultNodeTechnique("Default", DefaultTechnique);

            technique1.AddParameter(environment, NameOfInputDataProperty, typeof(object[][]), null,
                Ls.Literal("The source table as a 2D list of rows."));

            technique1.AddParameter(environment, NameOfGroupColumnIndexProperty, typeof(int), null,
                Ls.Literal("Column index containing the key value to group by."));
            technique1.AddParameter(environment, NameOfGroupReturnColumn1IndexProperty, typeof(int), null,
                Ls.Literal("First grouped column index to return."));
            technique1.AddParameter(environment, NameOfGroupReturnColumn2IndexProperty, typeof(int), null,
                Ls.Literal("Second grouped column index to return."));
            technique1.AddParameter(environment, NameOfGroupReturnColumn3IndexProperty, typeof(int), null,
                Ls.Literal("Third grouped column index to return."));

            technique1.AddParameter(environment, NameOfLookupColumnIndexProperty, typeof(int), null,
                Ls.Literal("Column index to lookup the same key value."));
            technique1.AddParameter(environment, NameOfLookupReturnColumn1IndexProperty, typeof(int), null,
                Ls.Literal("First lookup column index to return."));
            technique1.AddParameter(environment, NameOfLookupReturnColumn2IndexProperty, typeof(int), null,
                Ls.Literal("Second lookup column index to return."));
            technique1.AddParameter(environment, NameOfLookupReturnColumn3IndexProperty, typeof(int), null,
                Ls.Literal("Third lookup column index to return."));

            technique1.AddParameter(environment, NameOfResultProperty, typeof(object[][]), null,
                Ls.Literal("Rows with 7 values: [LookupValue, GroupCol1, GroupCol2, GroupCol3, LookupCol1, LookupCol2, LookupCol3]."), NodePortRole.TechniqueOutputOnly);
        }

        static NodeUpdateResult DefaultTechnique(UtilityNode node, NodeUpdateContext updateContext)
        {
            GroupAndLookupByValue currentNode = (GroupAndLookupByValue)node;

            object[][] inputData = currentNode.InputData;
            if (inputData == null || inputData.Length == 0)
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfInputDataProperty + " Error - InputData is empty");

            int maxColumnCount = inputData.Where(r => r != null).Select(r => r.Length).DefaultIfEmpty(0).Max();
            if (maxColumnCount == 0)
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfInputDataProperty + " Error - InputData contains no columns");

            int[] groupReturnColumns = currentNode.GetGroupReturnColumnIndexes();
            int[] lookupReturnColumns = currentNode.GetLookupReturnColumnIndexes();

            NodeUpdateResult invalidGroupValidationResult = ValidateColumns(maxColumnCount, currentNode.GroupColumnIndex, NameOfGroupColumnIndexProperty, groupReturnColumns,
                NameOfGroupReturnColumn1IndexProperty, NameOfGroupReturnColumn2IndexProperty, NameOfGroupReturnColumn3IndexProperty);
            if (invalidGroupValidationResult != null)
                return invalidGroupValidationResult;

            NodeUpdateResult invalidLookupValidationResult = ValidateColumns(maxColumnCount, currentNode.LookupColumnIndex, NameOfLookupColumnIndexProperty, lookupReturnColumns,
                NameOfLookupReturnColumn1IndexProperty, NameOfLookupReturnColumn2IndexProperty, NameOfLookupReturnColumn3IndexProperty);
            if (invalidLookupValidationResult != null)
                return invalidLookupValidationResult;

            Dictionary<string, object[]> groupedRows = BuildFirstMatchMap(inputData, currentNode.GroupColumnIndex, groupReturnColumns);
            Dictionary<string, object[]> lookupRows = BuildFirstMatchMap(inputData, currentNode.LookupColumnIndex, lookupReturnColumns);

            List<object[]> result = new List<object[]>();
            foreach (KeyValuePair<string, object[]> groupEntry in groupedRows)
            {
                if (!lookupRows.TryGetValue(groupEntry.Key, out object[] lookupValues))
                    continue;

                result.Add(BuildOutputRow(groupEntry.Key, groupEntry.Value, lookupValues));
            }

            currentNode.Result = result.ToArray();
            return NodeUpdateResult.Success;
        }

        static NodeUpdateResult ValidateColumns(int maxColumnCount, int keyColumn, string keyColumnPropertyName, int[] returnColumns, params string[] returnColumnPropertyNames)
        {
            if (!IsValidColumn(keyColumn, maxColumnCount))
                return new NodeUpdateResult.TechniqueInvalidArguments(keyColumnPropertyName + " Error - index is out of range");

            for (int i = 0; i < returnColumns.Length; i++)
            {
                if (!IsValidColumn(returnColumns[i], maxColumnCount))
                    return new NodeUpdateResult.TechniqueInvalidArguments(returnColumnPropertyNames[i] + " Error - index is out of range");
            }

            return null;
        }

        static object[] BuildOutputRow(string key, object[] groupValues, object[] lookupValues)
        {
            List<object> row = new List<object>();
            row.Add(key);
            row.AddRange(groupValues);
            row.AddRange(lookupValues);
            return row.ToArray();
        }

        static Dictionary<string, object[]> BuildFirstMatchMap(object[][] rows, int keyColumn, int[] returnColumns)
        {
            Dictionary<string, object[]> result = new Dictionary<string, object[]>();

            foreach (object[] row in rows)
            {
                if (row == null || row.Length <= keyColumn)
                    continue;

                string key = row[keyColumn]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(key) || result.ContainsKey(key))
                    continue;

                object[] values = returnColumns.Select(column => row.Length > column ? row[column] : null).ToArray();
                result.Add(key, values);
            }

            return result;
        }

        static bool IsValidColumn(int columnIndex, int maxColumnCount)
        {
            return columnIndex >= 0 && columnIndex < maxColumnCount;
        }

        internal new NodeState State
        {
            get { return (NodeState)base.State; }
        }

        protected override UtilityNode.NodeState GetInitialState(NodeTechniqueDetermination initialActiveTechniqueDetermination)
        {
            return new NodeState(this, initialActiveTechniqueDetermination);
        }

        // To add additional output values later, update this method and the corresponding
        // technique parameters/properties (for example GroupReturnColumn4Index).
        int[] GetGroupReturnColumnIndexes()
        {
            return new[] { GroupReturnColumn1Index, GroupReturnColumn2Index, GroupReturnColumn3Index };
        }

        // To add additional output values later, update this method and the corresponding
        // technique parameters/properties (for example LookupReturnColumn4Index).
        int[] GetLookupReturnColumnIndexes()
        {
            return new[] { LookupReturnColumn1Index, LookupReturnColumn2Index, LookupReturnColumn3Index };
        }

        public object[][] InputData
        {
            get { return State.InputDataProperty.GetNativeValue<object[][]>(); }
            set { State.InputDataProperty.SetNativeValueAndInputExpression(value); }
        }

        public int GroupColumnIndex
        {
            get { return State.GroupColumnIndexProperty.GetNativeValue<int>(); }
            set { State.GroupColumnIndexProperty.SetNativeValueAndInputExpression(value); }
        }

        public int GroupReturnColumn1Index
        {
            get { return State.GroupReturnColumn1IndexProperty.GetNativeValue<int>(); }
            set { State.GroupReturnColumn1IndexProperty.SetNativeValueAndInputExpression(value); }
        }

        public int GroupReturnColumn2Index
        {
            get { return State.GroupReturnColumn2IndexProperty.GetNativeValue<int>(); }
            set { State.GroupReturnColumn2IndexProperty.SetNativeValueAndInputExpression(value); }
        }

        public int GroupReturnColumn3Index
        {
            get { return State.GroupReturnColumn3IndexProperty.GetNativeValue<int>(); }
            set { State.GroupReturnColumn3IndexProperty.SetNativeValueAndInputExpression(value); }
        }

        public int LookupColumnIndex
        {
            get { return State.LookupColumnIndexProperty.GetNativeValue<int>(); }
            set { State.LookupColumnIndexProperty.SetNativeValueAndInputExpression(value); }
        }

        public int LookupReturnColumn1Index
        {
            get { return State.LookupReturnColumn1IndexProperty.GetNativeValue<int>(); }
            set { State.LookupReturnColumn1IndexProperty.SetNativeValueAndInputExpression(value); }
        }

        public int LookupReturnColumn2Index
        {
            get { return State.LookupReturnColumn2IndexProperty.GetNativeValue<int>(); }
            set { State.LookupReturnColumn2IndexProperty.SetNativeValueAndInputExpression(value); }
        }

        public int LookupReturnColumn3Index
        {
            get { return State.LookupReturnColumn3IndexProperty.GetNativeValue<int>(); }
            set { State.LookupReturnColumn3IndexProperty.SetNativeValueAndInputExpression(value); }
        }

        public object[][] Result
        {
            get { return State.ResultProperty.GetNativeValue<object[][]>(); }
            set { State.ResultProperty.SetNativeValueAndInputExpression(value); }
        }

        public new class NodeState : UtilityNode.NodeState
        {
            internal readonly UtilityNodeProperty InputDataProperty;
            internal readonly UtilityNodeProperty GroupColumnIndexProperty;
            internal readonly UtilityNodeProperty GroupReturnColumn1IndexProperty;
            internal readonly UtilityNodeProperty GroupReturnColumn2IndexProperty;
            internal readonly UtilityNodeProperty GroupReturnColumn3IndexProperty;
            internal readonly UtilityNodeProperty LookupColumnIndexProperty;
            internal readonly UtilityNodeProperty LookupReturnColumn1IndexProperty;
            internal readonly UtilityNodeProperty LookupReturnColumn2IndexProperty;
            internal readonly UtilityNodeProperty LookupReturnColumn3IndexProperty;
            internal readonly UtilityNodeProperty ResultProperty;

            internal protected NodeState(GroupAndLookupByValue parentNode, NodeTechniqueDetermination initialActiveTechniqueDetermination) :
                base(parentNode, initialActiveTechniqueDetermination)
            {
                InputDataProperty = AddProperty(NameOfInputDataProperty);
                GroupColumnIndexProperty = AddProperty(NameOfGroupColumnIndexProperty);
                GroupReturnColumn1IndexProperty = AddProperty(NameOfGroupReturnColumn1IndexProperty);
                GroupReturnColumn2IndexProperty = AddProperty(NameOfGroupReturnColumn2IndexProperty);
                GroupReturnColumn3IndexProperty = AddProperty(NameOfGroupReturnColumn3IndexProperty);
                LookupColumnIndexProperty = AddProperty(NameOfLookupColumnIndexProperty);
                LookupReturnColumn1IndexProperty = AddProperty(NameOfLookupReturnColumn1IndexProperty);
                LookupReturnColumn2IndexProperty = AddProperty(NameOfLookupReturnColumn2IndexProperty);
                LookupReturnColumn3IndexProperty = AddProperty(NameOfLookupReturnColumn3IndexProperty);
                ResultProperty = AddProperty(NameOfResultProperty);
            }

            protected NodeState(NodeState source) : base(source)
            {
                InputDataProperty = GetProperty(NameOfInputDataProperty);
                GroupColumnIndexProperty = GetProperty(NameOfGroupColumnIndexProperty);
                GroupReturnColumn1IndexProperty = GetProperty(NameOfGroupReturnColumn1IndexProperty);
                GroupReturnColumn2IndexProperty = GetProperty(NameOfGroupReturnColumn2IndexProperty);
                GroupReturnColumn3IndexProperty = GetProperty(NameOfGroupReturnColumn3IndexProperty);
                LookupColumnIndexProperty = GetProperty(NameOfLookupColumnIndexProperty);
                LookupReturnColumn1IndexProperty = GetProperty(NameOfLookupReturnColumn1IndexProperty);
                LookupReturnColumn2IndexProperty = GetProperty(NameOfLookupReturnColumn2IndexProperty);
                LookupReturnColumn3IndexProperty = GetProperty(NameOfLookupReturnColumn3IndexProperty);
                ResultProperty = GetProperty(NameOfResultProperty);
            }

            protected new GroupAndLookupByValue UtilityNode
            {
                get { return (GroupAndLookupByValue)base.UtilityNode(); }
            }

            public override UtilityNode.NodeState Clone()
            {
                return new NodeState(this);
            }

            public override bool TryGetDefaultOutputProperty(out INodeProperty property)
            {
                property = ResultProperty;
                return true;
            }
        }
    }
}
