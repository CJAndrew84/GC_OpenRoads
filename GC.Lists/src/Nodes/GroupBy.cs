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

namespace Atom.BentleyOpenRoads.GC
{
    [GCNamespace("List")]
    [GCNodeTypePaletteCategory("GC Lists")]
    [GCNodeTypeIcon("Resources/GroupBy.png")]
    [GCHideInheritedTechniques]
    public class GroupBy : UtilityNode
    {
        internal const string NameOfInputDataProperty = "InputData";
        internal const string NameOfColumnIndexProperty = "ColumnIndex";
        internal const string NameOfGroupValueProperty = "GroupValue";
        internal const string NameOfGroupKeysProperty = "GroupKeys";
        internal const string NameOfResultProperty = "Result";

        static readonly NodeGCType s_gcTypeOfAllInstances = (NodeGCType)GCTypeTools.GetGCType(UniversalGCEnvironment.TheOnlyInstance, typeof(GroupBy));

        static public NodeGCType GCTypeOfAllInstances
        {
            get { return s_gcTypeOfAllInstances; }
        }

        static void AddAdditionalMembersToGCType(IGCEnvironment environment, GCType gcType, NativeNamespaceTranslator namespaceTranslator)
        {
            UtilityNodeTechnique technique1 = gcType.AddDefaultNodeTechnique("Default", DefaultTechnique);

            technique1.AddParameter(environment, NameOfInputDataProperty, typeof(object[][]), null,
                                             Ls.Literal("The 2D list of data rows (e.g. from ReadExcel)"));

            technique1.AddParameter(environment, NameOfColumnIndexProperty, typeof(int), null,
                                              Ls.Literal("Zero-based index of the column to group by"));

            technique1.AddParameter(environment, NameOfGroupValueProperty, typeof(string), "",
                                              Ls.Literal("The column value to filter rows by"));

            technique1.AddParameter(environment, NameOfGroupKeysProperty, typeof(string[]), "",
                                  Ls.Literal("All unique values found in the specified column."), NodePortRole.TechniqueOutputOnly);

            technique1.AddParameter(environment, NameOfResultProperty, typeof(object[][]), null,
                                  Ls.Literal("Rows where the specified column matches GroupValue."), NodePortRole.TechniqueOutputOnly);
        }

        static NodeUpdateResult DefaultTechnique(UtilityNode node, NodeUpdateContext updateContext)
        {
            GroupBy groupBy = (GroupBy)node;

            object[][] inputData = groupBy.InputData;
            int columnIndex = groupBy.ColumnIndex;
            string groupValue = groupBy.GroupValue;

            if (inputData == null || inputData.Length == 0)
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfInputDataProperty + " Error - InputData is empty");

            if (columnIndex < 0 || columnIndex >= inputData[0].Length)
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfColumnIndexProperty + " Error - ColumnIndex is out of range");

            // Extract all unique values from the specified column, sorted
            string[] groupKeys = inputData
                .Where(row => row != null && row.Length > columnIndex)
                .Select(row => row[columnIndex]?.ToString() ?? "")
                .Distinct()
                .OrderBy(k => k)
                .ToArray();

            groupBy.GroupKeys = groupKeys;

            // Return all rows where the column value matches GroupValue
            object[][] result = inputData
                .Where(row => row != null && row.Length > columnIndex &&
                              (row[columnIndex]?.ToString() ?? "") == groupValue)
                .ToArray();

            groupBy.Result = result;

            return NodeUpdateResult.Success;
        }

        // Beginning of instance members.

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

        public int ColumnIndex
        {
            get { return State.ColumnIndexProperty.GetNativeValue<int>(); }
            set { State.ColumnIndexProperty.SetNativeValueAndInputExpression(value); }
        }

        public string GroupValue
        {
            get { return State.GroupValueProperty.GetNativeValue<string>(); }
            set { State.GroupValueProperty.SetNativeValueAndInputExpression(value); }
        }

        public string[] GroupKeys
        {
            get { return State.GroupKeysProperty.GetNativeValue<string[]>(); }
            set { State.GroupKeysProperty.SetNativeValueAndInputExpression(value); }
        }

        public object[][] Result
        {
            get { return State.ResultProperty.GetNativeValue<object[][]>(); }
            set { State.ResultProperty.SetNativeValueAndInputExpression(value); }
        }

        public new class NodeState : UtilityNode.NodeState
        {
            internal readonly UtilityNodeProperty InputDataProperty;
            internal readonly UtilityNodeProperty ColumnIndexProperty;
            internal readonly UtilityNodeProperty GroupValueProperty;
            internal readonly UtilityNodeProperty GroupKeysProperty;
            internal readonly UtilityNodeProperty ResultProperty;

            internal protected NodeState(GroupBy parentNode, NodeTechniqueDetermination initialActiveTechniqueDetermination) :
                                         base(parentNode, initialActiveTechniqueDetermination)
            {
                InputDataProperty = AddProperty(NameOfInputDataProperty);
                ColumnIndexProperty = AddProperty(NameOfColumnIndexProperty);
                GroupValueProperty = AddProperty(NameOfGroupValueProperty);
                GroupKeysProperty = AddProperty(NameOfGroupKeysProperty);
                ResultProperty = AddProperty(NameOfResultProperty);
            }

            protected NodeState(NodeState source) : base(source)
            {
                InputDataProperty = GetProperty(NameOfInputDataProperty);
                ColumnIndexProperty = GetProperty(NameOfColumnIndexProperty);
                GroupValueProperty = GetProperty(NameOfGroupValueProperty);
                GroupKeysProperty = GetProperty(NameOfGroupKeysProperty);
                ResultProperty = GetProperty(NameOfResultProperty);
            }

            protected new GroupBy UtilityNode
            {
                get { return (GroupBy)base.UtilityNode(); }
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
