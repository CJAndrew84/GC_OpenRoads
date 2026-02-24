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

namespace Atom.BentleyOpenRoads.GenDes
{
    [GCNamespace("List")]
    [GCNodeTypePaletteCategory("{Gen:Des} Lists")]
    [GCNodeTypeIcon("Resources/GroupBy.png")]
    [GCHideInheritedTechniques]
    public class GroupAndLookupByValue : UtilityNode
    {
        internal const string NameOfInputDataProperty = "InputData";
        internal const string NameOfMatchValueProperty = "MatchValue";

        internal const string NameOfGroupColumnIndexProperty = "GroupColumnIndex";
        internal const string NameOfGroupReturnColumn1IndexProperty = "GroupReturnColumn1Index";
        internal const string NameOfGroupReturnColumn2IndexProperty = "GroupReturnColumn2Index";
        internal const string NameOfGroupedResultProperty = "GroupedResult";

        internal const string NameOfLookupColumnIndexProperty = "LookupColumnIndex";
        internal const string NameOfLookupReturnColumn1IndexProperty = "LookupReturnColumn1Index";
        internal const string NameOfLookupReturnColumn2IndexProperty = "LookupReturnColumn2Index";
        internal const string NameOfLookupResultProperty = "LookupResult";

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
            technique1.AddParameter(environment, NameOfMatchValueProperty, typeof(string), "",
                Ls.Literal("Value used for grouping and lookup matching."));

            technique1.AddParameter(environment, NameOfGroupColumnIndexProperty, typeof(int), null,
                Ls.Literal("Column index used to group/filter rows by MatchValue."));
            technique1.AddParameter(environment, NameOfGroupReturnColumn1IndexProperty, typeof(int), null,
                Ls.Literal("First column index to return from grouped rows."));
            technique1.AddParameter(environment, NameOfGroupReturnColumn2IndexProperty, typeof(int), null,
                Ls.Literal("Second column index to return from grouped rows."));
            technique1.AddParameter(environment, NameOfGroupedResultProperty, typeof(object[][]), null,
                Ls.Literal("Grouped rows with only the two requested return columns."), NodePortRole.TechniqueOutputOnly);

            technique1.AddParameter(environment, NameOfLookupColumnIndexProperty, typeof(int), null,
                Ls.Literal("Column index used for lookup/filter by MatchValue."));
            technique1.AddParameter(environment, NameOfLookupReturnColumn1IndexProperty, typeof(int), null,
                Ls.Literal("First column index to return from lookup rows."));
            technique1.AddParameter(environment, NameOfLookupReturnColumn2IndexProperty, typeof(int), null,
                Ls.Literal("Second column index to return from lookup rows."));
            technique1.AddParameter(environment, NameOfLookupResultProperty, typeof(object[][]), null,
                Ls.Literal("Lookup rows with only the two requested return columns."), NodePortRole.TechniqueOutputOnly);
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

            if (!IsValidColumn(currentNode.GroupColumnIndex, maxColumnCount))
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfGroupColumnIndexProperty + " Error - index is out of range");
            if (!IsValidColumn(currentNode.GroupReturnColumn1Index, maxColumnCount))
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfGroupReturnColumn1IndexProperty + " Error - index is out of range");
            if (!IsValidColumn(currentNode.GroupReturnColumn2Index, maxColumnCount))
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfGroupReturnColumn2IndexProperty + " Error - index is out of range");
            if (!IsValidColumn(currentNode.LookupColumnIndex, maxColumnCount))
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfLookupColumnIndexProperty + " Error - index is out of range");
            if (!IsValidColumn(currentNode.LookupReturnColumn1Index, maxColumnCount))
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfLookupReturnColumn1IndexProperty + " Error - index is out of range");
            if (!IsValidColumn(currentNode.LookupReturnColumn2Index, maxColumnCount))
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfLookupReturnColumn2IndexProperty + " Error - index is out of range");

            string matchValue = currentNode.MatchValue ?? "";

            currentNode.GroupedResult = GetTwoColumnResult(inputData, currentNode.GroupColumnIndex, matchValue, currentNode.GroupReturnColumn1Index, currentNode.GroupReturnColumn2Index);
            currentNode.LookupResult = GetTwoColumnResult(inputData, currentNode.LookupColumnIndex, matchValue, currentNode.LookupReturnColumn1Index, currentNode.LookupReturnColumn2Index);

            return NodeUpdateResult.Success;
        }

        static bool IsValidColumn(int columnIndex, int maxColumnCount)
        {
            return columnIndex >= 0 && columnIndex < maxColumnCount;
        }

        static object[][] GetTwoColumnResult(object[][] rows, int matchColumn, string matchValue, int returnColumn1, int returnColumn2)
        {
            List<object[]> result = new List<object[]>();

            foreach (object[] row in rows)
            {
                if (row == null || row.Length <= matchColumn)
                    continue;

                string currentValue = row[matchColumn]?.ToString() ?? "";
                if (currentValue != matchValue)
                    continue;

                object firstValue = row.Length > returnColumn1 ? row[returnColumn1] : null;
                object secondValue = row.Length > returnColumn2 ? row[returnColumn2] : null;
                result.Add(new object[] { firstValue, secondValue });
            }

            return result.ToArray();
        }

        internal new NodeState State
        {
            get { return (NodeState)base.State; }
        }

        protected override UtilityNode.NodeState GetInitialState(NodeTechniqueDetermination initialActiveTechniqueDetermination)
        {
            return new NodeState(this, initialActiveTechniqueDetermination);
        }

        public object[][] InputData
        {
            get { return State.InputDataProperty.GetNativeValue<object[][]>(); }
            set { State.InputDataProperty.SetNativeValueAndInputExpression(value); }
        }

        public string MatchValue
        {
            get { return State.MatchValueProperty.GetNativeValue<string>(); }
            set { State.MatchValueProperty.SetNativeValueAndInputExpression(value); }
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

        public object[][] GroupedResult
        {
            get { return State.GroupedResultProperty.GetNativeValue<object[][]>(); }
            set { State.GroupedResultProperty.SetNativeValueAndInputExpression(value); }
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

        public object[][] LookupResult
        {
            get { return State.LookupResultProperty.GetNativeValue<object[][]>(); }
            set { State.LookupResultProperty.SetNativeValueAndInputExpression(value); }
        }

        public new class NodeState : UtilityNode.NodeState
        {
            internal readonly UtilityNodeProperty InputDataProperty;
            internal readonly UtilityNodeProperty MatchValueProperty;
            internal readonly UtilityNodeProperty GroupColumnIndexProperty;
            internal readonly UtilityNodeProperty GroupReturnColumn1IndexProperty;
            internal readonly UtilityNodeProperty GroupReturnColumn2IndexProperty;
            internal readonly UtilityNodeProperty GroupedResultProperty;
            internal readonly UtilityNodeProperty LookupColumnIndexProperty;
            internal readonly UtilityNodeProperty LookupReturnColumn1IndexProperty;
            internal readonly UtilityNodeProperty LookupReturnColumn2IndexProperty;
            internal readonly UtilityNodeProperty LookupResultProperty;

            internal protected NodeState(GroupAndLookupByValue parentNode, NodeTechniqueDetermination initialActiveTechniqueDetermination) :
                base(parentNode, initialActiveTechniqueDetermination)
            {
                InputDataProperty = AddProperty(NameOfInputDataProperty);
                MatchValueProperty = AddProperty(NameOfMatchValueProperty);
                GroupColumnIndexProperty = AddProperty(NameOfGroupColumnIndexProperty);
                GroupReturnColumn1IndexProperty = AddProperty(NameOfGroupReturnColumn1IndexProperty);
                GroupReturnColumn2IndexProperty = AddProperty(NameOfGroupReturnColumn2IndexProperty);
                GroupedResultProperty = AddProperty(NameOfGroupedResultProperty);
                LookupColumnIndexProperty = AddProperty(NameOfLookupColumnIndexProperty);
                LookupReturnColumn1IndexProperty = AddProperty(NameOfLookupReturnColumn1IndexProperty);
                LookupReturnColumn2IndexProperty = AddProperty(NameOfLookupReturnColumn2IndexProperty);
                LookupResultProperty = AddProperty(NameOfLookupResultProperty);
            }

            protected NodeState(NodeState source) : base(source)
            {
                InputDataProperty = GetProperty(NameOfInputDataProperty);
                MatchValueProperty = GetProperty(NameOfMatchValueProperty);
                GroupColumnIndexProperty = GetProperty(NameOfGroupColumnIndexProperty);
                GroupReturnColumn1IndexProperty = GetProperty(NameOfGroupReturnColumn1IndexProperty);
                GroupReturnColumn2IndexProperty = GetProperty(NameOfGroupReturnColumn2IndexProperty);
                GroupedResultProperty = GetProperty(NameOfGroupedResultProperty);
                LookupColumnIndexProperty = GetProperty(NameOfLookupColumnIndexProperty);
                LookupReturnColumn1IndexProperty = GetProperty(NameOfLookupReturnColumn1IndexProperty);
                LookupReturnColumn2IndexProperty = GetProperty(NameOfLookupReturnColumn2IndexProperty);
                LookupResultProperty = GetProperty(NameOfLookupResultProperty);
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
                property = GroupedResultProperty;
                return true;
            }
        }
    }
}
