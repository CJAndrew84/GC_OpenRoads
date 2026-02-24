using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.GCScript.GCTypes;
using Bentley.GenerativeComponents.GCScript.ReflectedNativeTypeSupport;
using Bentley.GenerativeComponents.GeneralPurpose;
using Bentley.GenerativeComponents.UtilityNodes;
using Bentley.GenerativeComponents.View;
using System;
using System.Collections.Generic;

namespace GenDes.Lists
{
    [GCNamespace("List")]
    [GCNodeTypePaletteCategory("{Gen:Des} Lists")]
    [GCNodeTypeIcon("Resources/ListFilter.png")]
    [GCHideInheritedTechniques]
    public class ListFilter : UtilityNode
    {
        internal const string NameOfInputListProperty = "InputList";
        internal const string NameOfFilterListProperty = "FilterList";
        internal const string NameOfResultListProperty = "ResultList";

        static readonly NodeGCType s_gcTypeOfAllInstances = (NodeGCType)GCTypeTools.GetGCType(UniversalGCEnvironment.TheOnlyInstance, typeof(ListFilter));

        static public NodeGCType GCTypeOfAllInstances
        {
            get { return s_gcTypeOfAllInstances; }
        }

        static void AddAdditionalMembersToGCType(IGCEnvironment environment, GCType gcType, NativeNamespaceTranslator namespaceTranslator)
        {
            UtilityNodeTechnique technique1 = gcType.AddDefaultNodeTechnique("Default", DefaultTechnique);

            technique1.AddParameter(environment, NameOfInputListProperty, typeof(string[]), "",
                                             Ls.Literal("The list to be filtered"));

            technique1.AddParameter(environment, NameOfFilterListProperty, typeof(Boolean[]), null,
                                              Ls.Literal("The filter list (Boolean list)"));

            technique1.AddParameter(environment, NameOfResultListProperty, typeof(string[]), "",
                                  Ls.Literal("The filtered list."), NodePortRole.TechniqueOutputOnly);
        }

        static NodeUpdateResult DefaultTechnique(UtilityNode node, NodeUpdateContext updateContext)
        {
            ListFilter listFilter = (ListFilter)node;  // It's safe to assume that the given node is of this class type
                                                       // (ListFilter).

            string[] mylist = listFilter.InputList;
            bool[] myfilter = listFilter.FilterList;
            List<string> myfilteredlist = new List<string>();

            if (mylist.Length > myfilter.Length)
                return new NodeUpdateResult.TechniqueInvalidArguments(NameOfFilterListProperty + " Error - Filter List is shorter than Input List");

            else
            {
                for (int i = 0; i < mylist.Length; i++)
                {
                    if (myfilter[i])
                    {
                        myfilteredlist.Add(mylist[i]);
                    }
                }

                listFilter.Result = myfilteredlist.ToArray();

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

        public string[] InputList
        {
            get { return State.InputListProperty.GetNativeValue<string[]>(); }
            set { State.InputListProperty.SetNativeValueAndInputExpression(value); }
        }

        public bool[] FilterList
        {
            get { return State.FilterListProperty.GetNativeValue<bool[]>(); }
            set { State.FilterListProperty.SetNativeValueAndInputExpression(value); }
        }

        public string[] Result
        {
            get { return State.ResultListProperty.GetNativeValue<string[]>(); }
            set { State.ResultListProperty.SetNativeValueAndInputExpression(value); }
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
            internal readonly UtilityNodeProperty FilterListProperty;
            internal readonly UtilityNodeProperty ResultListProperty;

            internal protected NodeState(ListFilter parentNode, NodeTechniqueDetermination initialActiveTechniqueDetermination) :
                                         base(parentNode, initialActiveTechniqueDetermination)
            {
                // IMPORTANT: This constructor calls 'AddProperty' to get each property field, whereas the following
                // constructor calls 'GetProperty'.

                InputListProperty = AddProperty(NameOfInputListProperty);
                FilterListProperty = AddProperty(NameOfFilterListProperty);
                ResultListProperty = AddProperty(NameOfResultListProperty);
            }

            protected NodeState(NodeState source) : base(source)
            {
                // IMPORTANT: This constructor calls 'GetProperty' to get each property field, whereas the preceding
                // constructor calls 'AddProperty'.

                InputListProperty = GetProperty(NameOfInputListProperty);
                FilterListProperty = GetProperty(NameOfFilterListProperty);
                ResultListProperty = GetProperty(NameOfResultListProperty);
            }

            protected new ListFilter UtilityNode
            {
                get { return (ListFilter)base.UtilityNode(); }
            }

            public override UtilityNode.NodeState Clone()
            {
                return new NodeState(this);
            }

            public override bool TryGetDefaultOutputProperty(out INodeProperty property)
            {
                property = ResultListProperty;
                return true;
            }
        }
    }
}