using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.View;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BGCscriptEd = Bentley.GenerativeComponents.ScriptEditor;

namespace Atom.BentleyOpenRoads.GC
{
    [GCNamespace("GC")]
    [GCNodeTypePaletteCategory("GC Add-in")]
    [GCNodeTypeIcon("Resources/json.png")]
    [GCSummary("Read and process any JSON file dynamically. Supports top-level inspection, " +
               "table extraction from arrays, single-column extraction, and JSONPath queries. " +
               "No fixed schema required - works with any JSON structure.")]
    public class JSON : GeometricNode
    {
        // ─────────────────────────────────────────────────────────────────────
        // Technique 1 (Default): ReadFile
        //   Reads any JSON file and exposes its top-level structure.
        //   If the root is an object  → PropertyNames / PropertyValues are populated.
        //   If the root is an array   → ItemCount is set; PropertyNames is empty.
        //   Use RawJson as input to downstream text/script nodes if needed.
        // ─────────────────────────────────────────────────────────────────────

        [GCDefaultTechnique]
        [GCSummary("Read any JSON file and output its top-level property names and values as strings. " +
                   "RawJson contains the full file text for use downstream.")]
        [GCParameter("JsonFilePath", "Path to the JSON file to read.")]
        public NodeUpdateResult ReadFile
        (
            NodeUpdateContext updateContext,

            [GCFileBrowser(
                buttonToolTip: "Browse for JSON File",
                dialogTitle: "Select JSON File",
                mode: BGCscriptEd.FileBrowserMode.InputFile,
                fileFilterDescription: "JSON Files",
                fileFilterMask: "*.json")]
            string JsonFilePath,

            [GCOut, GCInitiallyPinned] ref string   RawJson,
            [GCOut, GCInitiallyPinned] ref string[] PropertyNames,
            [GCOut, GCInitiallyPinned] ref string[] PropertyValues,
            [GCOut, GCInitiallyPinned] ref int      ItemCount
        )
        {
            try
            {
                string json = File.ReadAllText(JsonFilePath);
                RawJson = json;

                JToken root = JToken.Parse(json);

                if (root is JObject obj)
                {
                    var names  = new List<string>();
                    var values = new List<string>();

                    foreach (JProperty prop in obj.Properties())
                    {
                        names.Add(prop.Name);
                        // Serialize nested objects/arrays compactly; unwrap scalar values.
                        values.Add(UnwrapValue(prop.Value));
                    }

                    PropertyNames  = names.ToArray();
                    PropertyValues = values.ToArray();
                    ItemCount      = names.Count;
                }
                else if (root is JArray arr)
                {
                    PropertyNames  = new string[0];
                    PropertyValues = new string[0];
                    ItemCount      = arr.Count;
                }
                else
                {
                    // Scalar root (rare but valid JSON)
                    PropertyNames  = new string[0];
                    PropertyValues = new[] { root.ToString() };
                    ItemCount      = 1;
                }
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Technique 2: ReadTable
        //   Reads a JSON array of objects and returns tabular data.
        //
        //   ArrayPath  - JSONPath to reach the array (e.g. "$.features" or
        //                "$.data.items").  Leave blank / "$" for a root array.
        //   ColumnKeys - Comma-separated list of property names to extract as
        //                columns.  Leave blank to auto-detect from the first object.
        //   SchemaFilePath - Optional path to a schema JSON file:
        //                {
        //                  "arrayPath": "$.features",   // optional override
        //                  "columns":   ["id","name","x","y"]
        //                }
        //                When provided, SchemaFilePath takes precedence over
        //                inline ArrayPath / ColumnKeys inputs.
        //
        //   Outputs:
        //     Headers[]   - Column names in the order they were resolved.
        //     Rows[][]    - Jagged string array; each element is one data row.
        //                   GC replication on Rows gives one string[] per row.
        //     RowCount    - Number of data rows.
        //     ColumnCount - Number of columns.
        // ─────────────────────────────────────────────────────────────────────

        [GCSummary("Read a JSON array of objects as a table. Provide an optional JSONPath to the " +
                   "array, optional comma-separated column keys, and an optional schema JSON file. " +
                   "Rows[][] is replicatable - each replica receives one row as a string[].")]
        [GCParameter("JsonFilePath",   "Path to the JSON file to read.")]
        [GCParameter("ArrayPath",      "JSONPath to the array within the file (e.g. '$.items'). " +
                                       "Leave blank for a root-level array.")]
        [GCParameter("ColumnKeys",     "Comma-separated property names to extract as columns. " +
                                       "Leave blank to auto-detect from the first object " +
                                       "(or use a SchemaFilePath).")]
        [GCParameter("SchemaFilePath", "Optional path to a schema JSON file that defines arrayPath " +
                                       "and/or columns. Overrides ArrayPath and ColumnKeys when present.")]
        public NodeUpdateResult ReadTable
        (
            NodeUpdateContext updateContext,

            [GCFileBrowser(
                buttonToolTip: "Browse for JSON File",
                dialogTitle: "Select JSON File",
                mode: BGCscriptEd.FileBrowserMode.InputFile,
                fileFilterDescription: "JSON Files",
                fileFilterMask: "*.json")]
            string JsonFilePath,

            string ArrayPath,

            string ColumnKeys,

            [GCFileBrowser(
                buttonToolTip: "Browse for Schema JSON File (optional)",
                dialogTitle: "Select Schema JSON File",
                mode: BGCscriptEd.FileBrowserMode.InputFile,
                fileFilterDescription: "JSON Files",
                fileFilterMask: "*.json")]
            string SchemaFilePath,

            [GCOut, GCInitiallyPinned] ref string[]   Headers,
            [GCOut, GCInitiallyPinned] ref string[][] Rows,
            [GCOut, GCInitiallyPinned] ref int        RowCount,
            [GCOut, GCInitiallyPinned] ref int        ColumnCount
        )
        {
            try
            {
                // ── Resolve effective ArrayPath and ColumnKeys from schema ──
                string effectiveArrayPath  = ArrayPath;
                string effectiveColumnKeys = ColumnKeys;

                if (!string.IsNullOrWhiteSpace(SchemaFilePath) && File.Exists(SchemaFilePath))
                {
                    JObject schema = JObject.Parse(File.ReadAllText(SchemaFilePath));

                    if (schema["arrayPath"] is JToken apToken && apToken.Type != JTokenType.Null)
                        effectiveArrayPath = apToken.ToString();

                    if (schema["columns"] is JArray colArray)
                        effectiveColumnKeys = string.Join(",", colArray.Select(c => c.ToString()));
                }

                // ── Load and navigate to the target array ──
                string json = File.ReadAllText(JsonFilePath);
                JToken root = JToken.Parse(json);
                JArray array = ResolveArray(root, effectiveArrayPath);

                // ── Determine columns ──
                List<string> keys = ResolveColumnKeys(effectiveColumnKeys, array);

                Headers     = keys.ToArray();
                ColumnCount = keys.Count;

                // ── Build rows ──
                var rows = new List<string[]>();

                foreach (JToken item in array)
                {
                    var row = new string[keys.Count];

                    if (item is JObject obj)
                    {
                        for (int i = 0; i < keys.Count; i++)
                        {
                            JToken val = obj[keys[i]];
                            row[i] = val != null ? UnwrapValue(val) : string.Empty;
                        }
                    }
                    else
                    {
                        // Scalar array - put value in first column, blank the rest
                        row[0] = item.ToString();
                        for (int i = 1; i < row.Length; i++)
                            row[i] = string.Empty;
                    }

                    rows.Add(row);
                }

                Rows     = rows.ToArray();
                RowCount = rows.Count;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Technique 3: ExtractColumn
        //   Extracts a single named property from every object in a JSON array.
        //   Returns both string and numeric (double) representations.
        //   Useful for directly connecting a JSON field to a GC numeric input.
        // ─────────────────────────────────────────────────────────────────────

        [GCSummary("Extract a single property from every object in a JSON array. " +
                   "Returns StringValues[] and NumericValues[] (NaN for non-numeric entries).")]
        [GCParameter("JsonFilePath", "Path to the JSON file to read.")]
        [GCParameter("ArrayPath",    "JSONPath to the array (e.g. '$.stations'). Blank = root array.")]
        [GCParameter("ColumnKey",    "The property name to extract from each object (e.g. 'elevation').")]
        public NodeUpdateResult ExtractColumn
        (
            NodeUpdateContext updateContext,

            [GCFileBrowser(
                buttonToolTip: "Browse for JSON File",
                dialogTitle: "Select JSON File",
                mode: BGCscriptEd.FileBrowserMode.InputFile,
                fileFilterDescription: "JSON Files",
                fileFilterMask: "*.json")]
            string JsonFilePath,

            string ArrayPath,

            string ColumnKey,

            [GCOut, GCInitiallyPinned] ref string[] StringValues,
            [GCOut, GCInitiallyPinned] ref double[] NumericValues,
            [GCOut, GCInitiallyPinned] ref int      Count
        )
        {
            try
            {
                string json = File.ReadAllText(JsonFilePath);
                JToken root = JToken.Parse(json);
                JArray array = ResolveArray(root, ArrayPath);

                var strList = new List<string>();
                var numList = new List<double>();

                foreach (JToken item in array)
                {
                    string strVal;

                    if (item is JObject obj)
                    {
                        JToken val = obj[ColumnKey];
                        strVal = val != null ? UnwrapValue(val) : string.Empty;
                    }
                    else
                    {
                        // Scalar array element - return as-is
                        strVal = item.ToString();
                    }

                    strList.Add(strVal);
                    numList.Add(ParseDouble(strVal));
                }

                StringValues  = strList.ToArray();
                NumericValues = numList.ToArray();
                Count         = strList.Count;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Technique 4: QueryPath
        //   Evaluates a JSONPath expression against the JSON file and returns
        //   all matching tokens as string and numeric arrays.
        //   Supports wildcards, filters, recursive descent, etc.
        //   Examples:
        //     "$.alignments[*].name"          → all alignment names
        //     "$.stations[?(@.type=='CL')]"   → filtered objects (serialised)
        //     "$..elevation"                  → all elevation values anywhere
        // ─────────────────────────────────────────────────────────────────────

        [GCSummary("Evaluate a JSONPath expression against the file and return all matching values " +
                   "as string and numeric arrays. Supports wildcards, filters, and recursive descent.")]
        [GCParameter("JsonFilePath", "Path to the JSON file to read.")]
        [GCParameter("JsonPath",     "JSONPath expression (e.g. '$.alignments[*].name').")]
        public NodeUpdateResult QueryPath
        (
            NodeUpdateContext updateContext,

            [GCFileBrowser(
                buttonToolTip: "Browse for JSON File",
                dialogTitle: "Select JSON File",
                mode: BGCscriptEd.FileBrowserMode.InputFile,
                fileFilterDescription: "JSON Files",
                fileFilterMask: "*.json")]
            string JsonFilePath,

            string JsonPath,

            [GCOut, GCInitiallyPinned] ref string[] Values,
            [GCOut, GCInitiallyPinned] ref double[] NumericValues,
            [GCOut, GCInitiallyPinned] ref int      Count
        )
        {
            try
            {
                string json = File.ReadAllText(JsonFilePath);
                JToken root = JToken.Parse(json);

                IEnumerable<JToken> tokens = root.SelectTokens(JsonPath);

                var strList = new List<string>();
                var numList = new List<double>();

                foreach (JToken tok in tokens)
                {
                    string strVal = UnwrapValue(tok);
                    strList.Add(strVal);
                    numList.Add(ParseDouble(strVal));
                }

                Values        = strList.ToArray();
                NumericValues = numList.ToArray();
                Count         = strList.Count;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Navigate to a JArray using an optional JSONPath expression.
        /// Accepts null / empty / "$" as "use the root directly".
        /// </summary>
        private static JArray ResolveArray(JToken root, string arrayPath)
        {
            JToken target;

            if (string.IsNullOrWhiteSpace(arrayPath) || arrayPath.Trim() == "$")
            {
                target = root;
            }
            else
            {
                target = root.SelectToken(arrayPath);
                if (target == null)
                    throw new Exception($"JSONPath '{arrayPath}' did not match any token in the file.");
            }

            if (target is JArray arr)
                return arr;

            throw new Exception(
                $"The token at path '{arrayPath ?? "$"}' is a {target?.Type.ToString() ?? "null"}, " +
                $"not a JSON array. Provide an ArrayPath that points to a JSON array.");
        }

        /// <summary>
        /// Resolve the list of column keys from a comma-separated string,
        /// or auto-detect from the first object in the array when the string is blank.
        /// </summary>
        private static List<string> ResolveColumnKeys(string columnKeys, JArray array)
        {
            if (!string.IsNullOrWhiteSpace(columnKeys))
            {
                return columnKeys
                    .Split(',')
                    .Select(k => k.Trim())
                    .Where(k => k.Length > 0)
                    .ToList();
            }

            // Auto-detect from the first object
            JObject first = array.OfType<JObject>().FirstOrDefault();
            if (first != null)
                return first.Properties().Select(p => p.Name).ToList();

            return new List<string> { "value" };
        }

        /// <summary>
        /// Convert a JToken to a plain string:
        ///   - Scalar (string / number / bool / null) → the bare value string
        ///   - Object / Array → compact JSON serialisation
        /// </summary>
        private static string UnwrapValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return string.Empty;

            if (token is JValue jv)
                return jv.Value?.ToString() ?? string.Empty;

            // Object or Array - serialise compactly
            return token.ToString(Formatting.None);
        }

        /// <summary>
        /// Try to parse a string as a double using the invariant culture.
        /// Returns double.NaN if the string is not a valid number.
        /// </summary>
        private static double ParseDouble(string value)
        {
            if (double.TryParse(
                    value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double result))
            {
                return result;
            }
            return double.NaN;
        }
    }
}
