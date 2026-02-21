using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.GCScript.GCTypes;
using Bentley.GenerativeComponents.GCScript.ReflectedNativeTypeSupport;
using Bentley.GenerativeComponents.GeneralPurpose;
using Bentley.GenerativeComponents.UtilityNodes;
using Bentley.GenerativeComponents.View;
using System.Collections.Generic;

namespace Atom.BentleyOpenRoads.GenDes
{
    [GCNamespace("List")]
    [GCNodeTypePaletteCategory("{Gen:Des} Lists")]
    [GCNodeTypeIcon("Resources/SplitList.png")]
    [GCHideInheritedTechniques]
    public class SplitList : UtilityNode
    {
        internal const string NameOfInputListProperty = "InputList";
        internal const string NameOfSplitListProperty = "SplitPoint";
        internal const string NameOfResultList1Property = "ResultList1";
        internal const string NameOfResultList2Property = "ResultList2";

        static readonly NodeGCType s_gcTypeOfAllInstances = (NodeGCType)GCTypeTools.GetGCType(UniversalGCEnvironment.TheOnlyInstance, typeof(SplitList));

        static public NodeGCType GCTypeOfAllInstances
        {
            get { return s_gcTypeOfAllInstances; }
        }

        static void AddAdditionalMembersToGCType(IGCEnvironment environment, GCType gcType, NativeNamespaceTranslator namespaceTranslator)
        {
            UtilityNodeTechnique technique1 = gcType.AddDefaultNodeTechnique("Default", DefaultTechnique);

            technique1.AddParameter(environment, NameOfInputListProperty, typeof(object[]), "",
                                             Ls.Literal("The list to be filtered"));

            technique1.AddParameter(environment, NameOfSplitListProperty, typeof(int), null,
                                              Ls.Literal("The index point of split"));

            technique1.AddParameter(environment, NameOfResultList1Property, typeof(object[]), "",
                                  Ls.Literal("The first part of the list."), NodePortRole.TechniqueOutputOnly);

            technique1.AddParameter(environment, NameOfResultList2Property, typeof(object[]), "",
                      Ls.Literal("The second part of the list."), NodePortRole.TechniqueOutputOnly);
        }

        static NodeUpdateResult DefaultTechnique(UtilityNode node, NodeUpdateContext updateContext)
        {
            SplitList splitList = (SplitList)node;  // It's safe to assume that the given node is of this class type
                                                    // (ListFilter).

            object[] mylist = splitList.InputList;
            int maxValue = splitList.SplitPoint;
            List<object> mylist1 = new List<object>();
            List<object> mylist2 = new List<object>();

            if (maxValue >= mylist.Length)
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfSplitListProperty + " Error - Index is higher than InputList");

            else
            {
                for (int i = 0; i < mylist.Length; i++)
                {
                    if (mylist.Length - 1 <= maxValue)
                    {

                        mylist1.Add(mylist[i]);
                    }
                    else
                    {
                        mylist2.Add(mylist[i]);
                    }
                }

                splitList.Result1 = mylist1.ToArray();
                splitList.Result2 = mylist2.ToArray();

                return NodeUpdateResult.Success;
            }
        }

        // Beginning of instance members.

        // The following two methods -- ActiveNodeState and GetInitialNodeState -- are simply "must be defined"
        // methods that all Node-based node classes must have. These can simply be copied-and-pasted
        // into your own Node-based class (changing the name of the constructor, of course).

        internal new NodeState State
        {
            get { return (NodeState)base.State; }
        }

        protected override UtilityNode.NodeState GetInitialState(NodeTechniqueDetermination initialActiveTechniqueDetermination)
        {
            return new NodeState(this, initialActiveTechniqueDetermination);
        }

        public object[] InputList
        {
            get { return State.InputListProperty.GetNativeValue<object[]>(); }
            set { State.InputListProperty.SetNativeValueAndInputExpression(value); }
        }

        public int SplitPoint
        {
            get { return State.SplitListProperty.GetNativeValue<int>(); }
            set { State.SplitListProperty.SetNativeValueAndInputExpression(value); }
        }

        public object[] Result1
        {
            get { return State.ResultList1Property.GetNativeValue<object[]>(); }
            set { State.ResultList1Property.SetNativeValueAndInputExpression(value); }
        }
        public object[] Result2
        {
            get { return State.ResultList2Property.GetNativeValue<object[]>(); }
            set { State.ResultList2Property.SetNativeValueAndInputExpression(value); }
        }

        // Following is the embedded NodeState class that every Node-based class must have.
        //
        // This embedded class can be copied-and-pasted from this class to your own node type class, then you can replace the
        // list of properties with the inputs and outputs that are specific to your own class.

        public new class NodeState : UtilityNode.NodeState
        {
            // There must be one NodeProperty field for each unique input and output of your node type, regardless of which
            // technique(s) each input and output belongs to. The order of these fields is irrelevant; we suggest listing them
            // alphabetically.

            internal readonly UtilityNodeProperty InputListProperty;
            internal readonly UtilityNodeProperty SplitListProperty;
            internal readonly UtilityNodeProperty ResultList1Property;
            internal readonly UtilityNodeProperty ResultList2Property;

            internal protected NodeState(SplitList parentNode, NodeTechniqueDetermination initialActiveTechniqueDetermination) :
                                         base(parentNode, initialActiveTechniqueDetermination)
            {
                // IMPORTANT: This constructor calls 'AddProperty' to get each property field, whereas the following
                // constructor calls 'GetProperty'.

                InputListProperty = AddProperty(NameOfInputListProperty);
                SplitListProperty = AddProperty(NameOfSplitListProperty);
                ResultList1Property = AddProperty(NameOfResultList1Property);
                ResultList2Property = AddProperty(NameOfResultList2Property);
            }

            protected NodeState(NodeState source) : base(source)
            {
                // IMPORTANT: This constructor calls 'GetProperty' to get each property field, whereas the preceding
                // constructor calls 'AddProperty'.

                InputListProperty = GetProperty(NameOfInputListProperty);
                SplitListProperty = GetProperty(NameOfSplitListProperty);
                ResultList1Property = GetProperty(NameOfResultList1Property);
                ResultList2Property = GetProperty(NameOfResultList2Property);
            }

            protected new SplitList UtilityNode
            {
                get { return (SplitList)base.UtilityNode(); }
            }

            public override UtilityNode.NodeState Clone()
            {
                return new NodeState(this);
            }

            public override bool TryGetDefaultOutputProperty(out INodeProperty property)
            {
                property = ResultList1Property;
                return true;
            }
        }
    }
}