using System;
using System.Windows;
using System.Collections.Generic;
using System.ComponentModel;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.GCScript;
//using Bentley.GenerativeComponents.GCScript.NameScopes;
//using Bentley.GenerativeComponents.GeneralPurpose;
//using Bentley.GenerativeComponents.MicroStation;
using Bentley.GenerativeComponents.View;
using BDPnet = Bentley.DgnPlatformNET;
using Bentley.MstnPlatformNET;
using Bentley.DgnPlatformNET.DgnEC;
using Bentley.ECObjects.Schema;
using Bentley.ECObjects.Instance;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.DgnPlatformNET.Elements;
using Bentley.GenerativeComponents.GCScript.GCTypes;
using Bentley.GenerativeComponents.GeneralPurpose.Collections;
using Bentley.GenerativeComponents.GeneralPurpose;
using Bentley.GenerativeComponents.ScriptEditor.Controls.ExpressionEditor;
using System.Linq;
using Bentley.GenerativeComponents;
using Bentley.ECObjects.Expressions;
using Bentley.DgnPlatform;
using Newtonsoft.Json;
using BGCscriptEd = Bentley.GenerativeComponents.ScriptEditor;
using System.IO;
using System.Reflection.Emit;
using Newtonsoft.Json.Linq;
using Bentley.GenerativeComponents.UtilityNodes;
using System.Collections;
using Bentley.GeometryNET;

namespace Atom.BentleyOpenRoads.GC
{

    [GCNamespace("CoDe")]
    [GCNodeTypePaletteCategory("{CoDe} GC Add-in")]
    [GCNodeTypeIcon("Resources/json.png")]
    [GCSummary("Read, write to JSON files")]

    public class JSON : GeometricNode //UtilityNode
    {
        private static readonly BDPnet.DgnFile _activeDgnFile = Session.Instance.GetActiveDgnFile();
        private static readonly BDPnet.DgnModel _activeDgnModel = Session.Instance.GetActiveDgnModel();

        [GCDefaultTechnique]
        [GCSummary("Read Atom Alignment JSON File")]
        [GCParameter("AtomAlignmentJSON", "Read Atom Alignment JSON.")]

        public NodeUpdateResult ReadJSONAlignments
        (
            NodeUpdateContext updateContext,
            [GCFileBrowser(
            buttonToolTip: "Browse for ATOM Alignment JSON File",
            dialogTitle:"Select ATOM Alignment JSON File",
            mode:BGCscriptEd.FileBrowserMode.InputFile,
            fileFilterDescription:"JSON Files",
            fileFilterMask:"*.json"
            )] string JSONFilePath,
            [GCOut, GCInitiallyPinned] ref string[] alignmentName,
            [GCOut, GCInitiallyPinned] ref DPoint3d[][] PIs,
            [GCOut, GCInitiallyPinned] ref double[][] Radii
        )
        {
            string jsonStr = "";

            var serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.Indented,
            };

            try
            {
                jsonStr = File.ReadAllText(JSONFilePath);

                List<Atom.Civil.Alignment> alignments = JsonConvert.DeserializeObject<List<Atom.Civil.Alignment>>(jsonStr, serializerSettings);

                List<string> alignmentList = new List<string>();

                //List<List<DPoint3d>> PI = new List<List<DPoint3d>>();
                DPoint3d[][] PI = new DPoint3d[alignments.Count][];
                List<DPoint3d[]> dPoint3Ds1 = new List<DPoint3d[]>();

                //List<List<double>> r = new List<List<double>>();
                double[][] r = new double[alignments.Count][];
                List<double[]> doubles1 = new List<double[]>();

                foreach (var item in alignments)
                {
                    alignmentList.Add(item.Name);
                    List<DPoint3d> points = new List<DPoint3d>();
                    List<double> radii = new List<double>();
                    foreach (var entity in item.Entities)
                    {
                        if (entity != null)
                        {
                            DPoint3d stpt = new DPoint3d();
                            stpt.X = entity.StartPoint.X;
                            stpt.Y = entity.StartPoint.Y;
                            stpt.Z = entity.StartPoint.Z;

                            points.Add(stpt);

                            if (entity.PI != null)
                            {
                                DPoint3d pipt = new DPoint3d();
                                pipt.X = entity.PI.X;
                                pipt.Y = entity.PI.Y;
                                pipt.Z = entity.PI.Z;

                                points.Add(pipt);
                            }//
                            if (entity.MidPoint != null)
                            {
                                DPoint3d mdpt = new DPoint3d();
                                mdpt.X = entity.MidPoint.X;
                                mdpt.Y = entity.MidPoint.Y;
                                mdpt.Z = entity.MidPoint.Z;

                                points.Add(mdpt);
                            }
                            if (entity.Radius != null)
                            {
                                double radius = entity.Radius;

                                radii.Add(radius);
                            }

                            DPoint3d edpt = new DPoint3d();
                            edpt.X = entity.EndPoint.X;
                            edpt.Y = entity.EndPoint.Y;
                            edpt.Z = entity.EndPoint.Z;

                            points.Add(edpt);
                        }
                    }
                    foreach (var i in item.PIs)
                    {
                        DPoint3d dPoint3D = new DPoint3d();
                        dPoint3D.X = i.Point.X;
                        dPoint3D.Y = i.Point.Y;
                        dPoint3D.Z = i.Point.Z;

                        points.Add(dPoint3D);

                        double radius = i.Radius;

                        radii.Add(radius);
                    }//
                    DPoint3d[] dPoint3Ds = new DPoint3d[points.Count];
                    points.CopyTo(dPoint3Ds);
                    double[] doubles = new double[radii.Count];
                    radii.CopyTo(doubles);
                    dPoint3Ds1.Add(dPoint3Ds);
                    doubles1.Add(doubles);

                }
                alignmentName = alignmentList.ToArray();
                dPoint3Ds1.CopyTo(PI);
                doubles1.CopyTo(r);
                PIs = PI;
                Radii = r;

            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;
        }
    }
}