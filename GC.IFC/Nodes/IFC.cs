using Atom.BentleyOpenRoads.Ops;
using Atom.BentleyOpenRoads.Ops.Helpers;
using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.ScriptEditor;
using Bentley.GenerativeComponents.ScriptEditor.Controls.ExpressionEditor;
using Bentley.GenerativeComponents.UtilityNodes;
using Bentley.GenerativeComponents.View;
using Bentley.MstnPlatformNET;
using GeometryGym.Ifc;
using System;
using System.Collections.Generic;
using System.Linq;
using BDPnet = Bentley.DgnPlatformNET;

namespace Atom.BentleyOpenRoads.GC
{
    //using static Bentley.GeometryNET.Polynomials;
    using EECC = ExpressionEditorCustomConfiguration;

    [GCNamespace("GCIFC")]
    [GCNodeTypePaletteCategory("GC IFC")]
    [GCNodeTypeIcon("Resources/ifc.png")]
    [GCHideInheritedTechniques]
    [GCSummary("Define and generate IFCs")]
    public class GCIFC : ElementBasedNode
    {
        private static DatabaseIfc newdb;
        private static int nestTest = 0;
        private static int nestDepth = 2;

        #region supporting methods
        static EECC GetEECCIfcVersion()
        {
            return new EECC(getList, ScriptChoiceOptions.UserMayOverrideChoices);

            IEnumerable<ScriptChoice> getList(ExpressionEditor parentExpressionEditor)
            {
                GCIFC node = (GCIFC)parentExpressionEditor.MeaningOfThis;
                ScriptChoiceList result = new ScriptChoiceList();
                foreach (var item in Enum.GetValues(typeof(ReleaseVersion)).Cast<ReleaseVersion>().ToList())
                {
                    result.Add(new ScriptChoice(item.ToString()));
                }
                return result;
            }
        }

        public void SetStrings(Civil.CorridorStrings strng, IfcSite ifcSite, bool isContour)
        {
            int i = 0;
            foreach (var segment in strng.Segments)
            {
                List<Tuple<double, double, double>> points = new List<Tuple<double, double, double>>();

                foreach (var vertex in segment.Vertex)
                {
                    Tuple<double, double, double> coordsdouble = new Tuple<double, double, double>(vertex.X, vertex.Y, vertex.Z);
                    points.Add(coordsdouble);
                }

                IfcPolyline polyline = new IfcPolyline(newdb, points);
                IfcProductDefinitionShape product1 = new IfcProductDefinitionShape(new IfcShapeRepresentation(polyline));
                IfcAnnotation ifcAnnotation = new IfcAnnotation(ifcSite, ifcSite.ObjectPlacement, product1);
                ifcAnnotation.Name = strng.Name + " " + i;
                ifcAnnotation.Description = strng.FeatureDefinition;
                if (isContour)
                {
                    ifcAnnotation.PredefinedType = IfcAnnotationTypeEnum.USERDEFINED;
                    ifcAnnotation.ObjectType = "ContourLine";
                    Pset_AnnotationContourLine pset_AnnotationContourLine = new Pset_AnnotationContourLine(ifcAnnotation);
                    pset_AnnotationContourLine.ContourValue = double.Parse(strng.Name.Split(new string[] { ":" }, StringSplitOptions.None)[1].Split(new string[] { "_" }, StringSplitOptions.None)[0]);
                }

                IfcColourRgb colour1 = new IfcColourRgb(newdb, strng.Colour);
                IfcSurfaceStyleRendering rend = new IfcSurfaceStyleRendering(colour1);
                IfcSurfaceStyle style = new IfcSurfaceStyle(rend);

                IfcPresentationLayerAssignment layer = new IfcPresentationLayerAssignment(strng.Layer, polyline);
                IfcStyledItem backfillstyledItem = new IfcStyledItem(polyline, style);

                foreach (var propset in strng.Properties)
                {
                    SetPropertySets(propset, ifcAnnotation);
                }

                ifcSite.AddAggregated(ifcAnnotation);
                i++;
            }
        }

        public void GetModels(BDPnet.DgnAttachment dgnAttachment, IfcSite ifcSite)
        {
            var Refcorridors = Atom.BentleyOpenRoads.Ops.CorridorOps.GetAllRef(dgnAttachment);
            if (Refcorridors.Count() > 0)
            {
                foreach (var corridor in Refcorridors)
                {
                    var strings = corridor.CorridorStrings;

                    foreach (var strng in strings)
                    {
                        if (!strng.Layer.Contains("Contour"))
                        {
                            List<IfcSegmentIndexSelect> segments = new List<IfcSegmentIndexSelect>();

                            try
                            {
                                var parts = strng.Entities;
                                if (parts != null)
                                {

                                }
                                else if (strng.Segments != null)
                                {
                                    SetStrings(strng, ifcSite, false);
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }
                        else
                        {
                            SetStrings(strng, ifcSite, true);
                        }
                    }

                    var surfaces = corridor.CorridorSurfaces;

                    foreach (var surface in surfaces)
                    {
                        SetComponents(surface, ifcSite);
                    }
                }
            }
            nestTest = 1;
            if (nestDepth > 0 && nestTest < nestDepth)
            {
                var models = dgnAttachment.GetDgnAttachments();
                foreach (var model in models)
                {
                    GetModels(model, ifcSite);
                }
                nestTest++;
            }
        }

        public void SetComponents(Civil.CorridorSurfaces surface, IfcSite ifcSite)
        {
            var pointindex = surface.PointIndex;

            List<string> items = new List<string>();
            foreach (int index in pointindex)
            {
                string v1 = Convert.ToString(index);
                items.Add(v1);
            }
            string delimiter = ",";
            string result = items.Select(i => i).Aggregate((i, j) => i + delimiter + j);

            var indexgroups = result.Split(new string[] { ",0," }, StringSplitOptions.None);

            List<IfcIndexedPolygonalFace> faces = new List<IfcIndexedPolygonalFace>();

            foreach (var group in indexgroups)
            {
                var indexlist = group.Split(',');
                ICollection<int> pi = new List<int>();
                foreach (var index in indexlist)
                {
                    int v2 = Convert.ToInt32(index);
                    pi.Add(v2);
                }
                IfcIndexedPolygonalFace face = new IfcIndexedPolygonalFace(newdb, pi);
                faces.Add(face);

            }

            var coords = new List<Tuple<double, double, double>>();
            foreach (var point in surface.Points)
            {
                coords.Add(Converters.PointConverter2(point));
            }

            IfcCartesianPointList3D pointlists = new IfcCartesianPointList3D(newdb, coords);

            IfcPolygonalFaceSet faceSet = new IfcPolygonalFaceSet(pointlists, faces);
            faceSet.Closed = true;

            IfcShapeRepresentation shape = new IfcShapeRepresentation(faceSet);
            IfcProductDefinitionShape product1 = new IfcProductDefinitionShape(shape);

            string surfacename = surface.Name;

            IfcBuiltElement ifcAnnotation = new IfcBuiltElement(ifcSite, ifcSite.ObjectPlacement, product1);
            ifcAnnotation.Name = surface.Name;
            ifcAnnotation.Description = surface.FeatureDefinition;

            ifcAnnotation.Representation.Representations.Add(shape);

            IfcColourRgb colour1 = new IfcColourRgb(newdb, surface.Colour);
            IfcSurfaceStyleRendering rend = new IfcSurfaceStyleRendering(colour1);
            IfcSurfaceStyle style = new IfcSurfaceStyle(rend);

            IfcPresentationLayerAssignment layer = new IfcPresentationLayerAssignment(surface.Layer, faceSet);
            IfcStyledItem backfillstyledItem = new IfcStyledItem(faceSet, style);
            newdb.Context.AddDeclared(ifcAnnotation);

            foreach (var propset in surface.Properties)
            {
                SetPropertySets(propset, ifcAnnotation);
            }

            ifcSite.AddAggregated(ifcAnnotation);
        }

        public void SetPropertySets(Civil.PropertySet propertySet, IfcObject ifcElement)
        {
            List<IfcProperty> ifcProp = new List<IfcProperty>();
            foreach (var pro in propertySet.properties)
            {
                ifcProp.Add(new IfcPropertySingleValue(newdb, pro.propertyName, new IfcIdentifier(pro.propertyValue.ToString())));
            }

            new IfcPropertySet(ifcElement, propertySet.Name, ifcProp);
        }

        #endregion

        [GCDefaultTechnique]
        [GCSummary("Select IFC Version and generate IFCDatabase")]
        public NodeUpdateResult IfcDatabase
        (
            NodeUpdateContext updateContext,
            [GCIn] string IFCVersion,
            [GCOut, GCInitiallyPinned] ref DatabaseIfc DatabaseIFC
        )
        {
            try
            {
                ReleaseVersion releaseVersion = (ReleaseVersion)Enum.Parse(typeof(ReleaseVersion), IFCVersion, true);
                newdb = new DatabaseIfc(releaseVersion);

                DatabaseIFC = newdb;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;

        }
        /*
        [GCTechnique]
        [GCSummary("Export IFC Bentley")]
        public NodeUpdateResult BentleyIfcExport
        (
            NodeUpdateContext updateContext,
            [GCIn, GCInitiallyPinned] string Version,
            [GCIn, GCInitiallyPinned] bool NamedBoundaries,
            [GCFileBrowser(FileBrowserMode.FolderOnly)] string folderpath,
            [GCIn, GCInitiallyPinned] string exportFileName
        )
        {
            try
            {

            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;
        }
        */

        [GCTechnique]
        [GCSummary("Set IfcSite")]
        public NodeUpdateResult SetSite
        (
            NodeUpdateContext updateContext,
            [GCIn, GCInitiallyPinned] DatabaseIfc DatabaseIFC,
            [GCIn, GCInitiallyPinned] string siteName,
            [GCIn, GCInitiallyPinned] string siteDescription,
            [GCOut, GCInitiallyPinned] ref IfcSite ifcSite
        )
        {
            try
            {
                ifcSite = new IfcSite(DatabaseIFC, siteName);
                ifcSite.Description = siteDescription;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;
        }

        [GCTechnique]
        [GCSummary("Set IfcProject")]
        public NodeUpdateResult SetProject
        (
            NodeUpdateContext updateContext,
            [GCIn, GCInitiallyPinned] IfcSite IfcSite,
            [GCIn, GCInitiallyPinned] string ProjectName,
            [GCIn, GCInitiallyPinned] string ProjectDescription,
            [GCOut, GCInitiallyPinned] ref IfcProject ifcProject
        )
        {
            try
            {
                ifcProject = new IfcProject(IfcSite, ProjectName, IfcUnitAssignment.Length.Metre);
                ifcProject.Name = ProjectName;
                ifcProject.Description = ProjectDescription;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;
        }

        [GCTechnique]
        [GCSummary("IFC Content")]
        public NodeUpdateResult IFCContent
        (
            NodeUpdateContext updateContext,
            [GCIn, GCInitiallyPinned] IfcSite ifcSite,
            [GCIn, GCInitiallyPinned] bool includeReferences,
            [GCIn, GCInitialValue(2)] int NestDepth
        )
        {
            try
            {
                nestDepth = NestDepth;
                nestTest = 0;
                bool version43 = false;
                if (newdb.Release.ToString() == "IFC4x3") { version43 = true; } else { version43 = false; }

                if (includeReferences)
                {
                    var dgnModelRef = Session.Instance.GetActiveDgnModelRef().GetDgnAttachments();

                    foreach (var modelRef in dgnModelRef)
                    {
                        GetModels(modelRef, ifcSite);
                        nestTest = 0;
                    }
                }
                else
                {
                    var corridors = CorridorOps.GetAll();

                    foreach (var corridor in corridors)
                    {
                        var strings = corridor.CorridorStrings;

                        foreach (var strng in strings)
                        {
                            if (!strng.Layer.Contains("Contour"))
                            {
                                List<IfcSegmentIndexSelect> segments = new List<IfcSegmentIndexSelect>();

                                try
                                {
                                    var parts = strng.Entities;
                                    if (parts != null)
                                    {

                                    }
                                    else if (strng.Segments != null)
                                    {
                                        SetStrings(strng, ifcSite, false);
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                SetStrings(strng, ifcSite, true);
                            }
                        }

                        var surfaces = corridor.CorridorSurfaces;

                        foreach (var surface in surfaces)
                        {
                            SetComponents(surface, ifcSite);
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

        [GCTechnique]
        [GCSummary("Save IFC")]
        public NodeUpdateResult SaveIFC
        (
            NodeUpdateContext updateContext,
            [GCIn, GCInitiallyPinned] DatabaseIfc databaseIfc,
            [GCFileBrowser(FileBrowserMode.FolderOnly)] string folderpath,
            [GCIn, GCInitiallyPinned] string exportFileName,
            [GCIn] string extension
        )
        {
            try
            {
                databaseIfc.WriteFile(folderpath + exportFileName + "." + extension);
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;
        }
    }
}
