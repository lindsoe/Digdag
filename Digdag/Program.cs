using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Features;
using NetTopologySuite.Precision;
using NetTopologySuite.IO.ShapeFile.Extended;
using NetTopologySuite.Simplify;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
namespace Digdag
{
    internal class Program
    {
        class Item
        {
            public int Digits { get; set; }
            public Double DistanceTolerance { get; set; }
        }

        static void unzipFiles(string mainDirectory)
        {
            ZipFile.ExtractToDirectory(Path.Combine(mainDirectory,@"AMTSLIG_REGIONAL_SHAPE_UTM32-EUREF89.zip"), Path.Combine(mainDirectory,"Amtslig"));
            ZipFile.ExtractToDirectory(Path.Combine(mainDirectory, @"GEOGRAFISK_SHAPE_UTM32-EUREF89.zip"), Path.Combine(mainDirectory, "Geografisk"));
            ZipFile.ExtractToDirectory(Path.Combine(mainDirectory, @"KIRKELIG_SHAPE_UTM32-EUREF89.zip"), Path.Combine(mainDirectory, "Kirkelig"));
            ZipFile.ExtractToDirectory(Path.Combine(mainDirectory, @"KOMMUNAL_SHAPE_UTM32-EUREF89.zip"), Path.Combine(mainDirectory, "Kommunal"));
            ZipFile.ExtractToDirectory(Path.Combine(mainDirectory, @"OVRIGE_SHAPE_UTM32-EUREF89.zip"), Path.Combine(mainDirectory, "Øvrige"));
            ZipFile.ExtractToDirectory(Path.Combine(mainDirectory, @"POLITIVAESEN_SHAPE_UTM32-EUREF89.zip"), Path.Combine(mainDirectory, "Politivæsen"));
            ZipFile.ExtractToDirectory(Path.Combine(mainDirectory, @"RETSLIG_SHAPE_UTM32-EUREF89.zip"), Path.Combine(mainDirectory, "Retslig"));
        }

        static void ReprojectAll(string mainDirectory)
        {
            string[] shapeFiles = Directory.GetFileSystemEntries(mainDirectory, "*.shp", SearchOption.AllDirectories);
            foreach (var shapeFile in shapeFiles)
            {
                try
                {
                    DotSpatial.Data.Shapefile sf = DotSpatial.Data.Shapefile.OpenFile(shapeFile);
                    if ((shapeFile.Contains("stift_sjl") || (shapeFile.Contains("stift_jyl"))) || (shapeFile.Contains("Overret_sjl") || (shapeFile.Contains("Overret_jyl")))) {
                        sf.DataTable.Columns["NAVN"].ColumnName = "navn";
                        sf.DataTable.Columns["OBJECTID"].ColumnName = "objectid";
                        sf.DataTable.Columns["ENHEDID"].ColumnName = "enhedid";
                        sf.DataTable.Columns["FRA"].ColumnName = "fra";
                        sf.DataTable.Columns["TIL"].ColumnName = "til";
                        sf.DataTable.Columns["SHAPE_AREA"].ColumnName = "SHAPE_Area";
                        sf.DataTable.Columns["SHAPE_LENG"].ColumnName = "SHAPE_Leng";
                    }
                    sf.Reproject(DotSpatial.Projections.KnownCoordinateSystems.Geographic.World.WGS1984);
                    sf.Save();
                    Console.WriteLine($"Reprojected {shapeFile}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fejl for {shapeFile}. ({ex.Message})");
                }
            }
        }



        static void ConvertShapefileToGeojson(string shapeFilename, string geojsonFilename, double distanceTolerance = 0.001, int digits = 3)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using (StreamWriter outputFile = new StreamWriter(geojsonFilename, false, System.Text.Encoding.UTF8))
            {
                int scale = int.Parse("1" + new string('0', digits));
                var shp = new ShapeDataReader(shapeFilename);
                var  shapefileFeatures = shp.ReadByMBRFilter(shp.ShapefileBounds);

                FeatureCollection featureCollection = new FeatureCollection();
                var jsonWriter = new GeoJsonWriter();

                var pm = new PrecisionModel(scale);
                var reducer = new GeometryPrecisionReducer(pm);
                foreach (var shapeFeature in shapefileFeatures)
                {
                    List<Coordinate> bounds = new List<Coordinate>();
                        bounds.Add(new Coordinate(shapeFeature.BoundingBox.MinY, shapeFeature.BoundingBox.MinX));
                        bounds.Add(new Coordinate(shapeFeature.BoundingBox.MaxY, shapeFeature.BoundingBox.MaxX));
                        shapeFeature.Attributes.Add("Interior", new Coordinate(shapeFeature.Geometry.InteriorPoint.Coordinate.Y, shapeFeature.Geometry.InteriorPoint.Coordinate.X));
                        shapeFeature.Attributes.Add("Centroid", new Coordinate(shapeFeature.Geometry.Centroid.Coordinate.Y, shapeFeature.Geometry.Centroid.Coordinate.X));
                        shapeFeature.Attributes.Add("Envelope", bounds);
                        shapeFeature.Attributes["SHAPE_Area"] = (Convert.ToDouble(shapeFeature.Attributes["SHAPE_Area"]) / 1000000).ToString("N0");
                        shapeFeature.Attributes["SHAPE_Leng"] = (Convert.ToDouble(shapeFeature.Attributes["SHAPE_Leng"]) / 1000).ToString("N0");
                        Feature feature = new GeoJsonReader().Read<Feature>(jsonWriter.Write(shapeFeature));
                        DouglasPeuckerSimplifier simplifier = new DouglasPeuckerSimplifier(feature.Geometry);
                        simplifier.DistanceTolerance = distanceTolerance;
                        simplifier.EnsureValidTopology = true;
                        Geometry tempGeom = reducer.Reduce(simplifier.GetResultGeometry());
                        // Hvis polygon er så lille, at reduktion fjerner alle koordinater, så undlad simplificering 
                        if (tempGeom.Coordinates.Count() > 0)
                        {
                            feature.Geometry = tempGeom;
                        }
                        featureCollection.Add(feature);
                }
                string jsonString = jsonWriter.Write(featureCollection);
                outputFile.WriteLine(jsonString);
            }
        }

        static void ConvertShapefileToGeojson(string shapeFilename, string geojsonFilename, int startYear, int endYear, int step, double distanceTolerance = 0.001, int digits = 3)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using (StreamWriter outputFile = new StreamWriter(geojsonFilename, false, System.Text.Encoding.UTF8))
            {
                int scale = int.Parse("1" + new string('0', digits));
                var shp = new ShapeDataReader(shapeFilename);
                var shapefileFeatures = shp.ReadByMBRFilter(shp.ShapefileBounds);

                FeatureCollection featureCollection = new FeatureCollection();
                var jsonWriter = new GeoJsonWriter();

                var pm = new PrecisionModel(scale);
                var reducer = new GeometryPrecisionReducer(pm);
                for (int i = startYear; i < endYear + 1;  i+= step)
                {
                    string year = i.ToString() + "-01-01";
                    foreach (var (shapeFeature, counter) in shapefileFeatures
                        .Where(x => x.Attributes["fra"].ToString().CompareTo(year) <= 0 && x.Attributes["til"].ToString().CompareTo(year) >= 0)
                        .Select((value, counter) => (value, counter)))
                    {
                        List<Coordinate> bounds = new List<Coordinate>();
                    bounds.Add(new Coordinate(shapeFeature.BoundingBox.MinY, shapeFeature.BoundingBox.MinX));
                    bounds.Add(new Coordinate(shapeFeature.BoundingBox.MaxY, shapeFeature.BoundingBox.MaxX));
                    shapeFeature.Attributes.Add("Interior", new Coordinate(shapeFeature.Geometry.InteriorPoint.Coordinate.Y, shapeFeature.Geometry.InteriorPoint.Coordinate.X));
                    shapeFeature.Attributes.Add("Centroid", new Coordinate(shapeFeature.Geometry.Centroid.Coordinate.Y, shapeFeature.Geometry.Centroid.Coordinate.X));
                    shapeFeature.Attributes.Add("Envelope", bounds);
                    shapeFeature.Attributes["SHAPE_Area"] = (Convert.ToDouble(shapeFeature.Attributes["SHAPE_Area"]) / 1000000).ToString("N0");
                    shapeFeature.Attributes["SHAPE_Leng"] = (Convert.ToDouble(shapeFeature.Attributes["SHAPE_Leng"]) / 1000).ToString("N0");
                    Feature feature = new GeoJsonReader().Read<Feature>(jsonWriter.Write(shapeFeature));
                    DouglasPeuckerSimplifier simplifier = new DouglasPeuckerSimplifier(feature.Geometry);
                    simplifier.DistanceTolerance = distanceTolerance;
                    simplifier.EnsureValidTopology = true;
                    Geometry tempGeom = reducer.Reduce(simplifier.GetResultGeometry());
                    // Hvis polygon er så lille, at reduktion fjerner alle koordinater, så undlad simplificering 
                    if (tempGeom.Coordinates.Count() > 0)
                    {
                        feature.Geometry = tempGeom;
                    }
                    featureCollection.Add(feature);
                }
                }
                string jsonString = jsonWriter.Write(featureCollection);
                outputFile.WriteLine(jsonString);
            }
        }


        static void Main(string[] args)
        {
            unzipFiles(@"F:\DIGDAG");
            ReprojectAll(@"F:\Digdag");
            string outputDir = @"C:\Users\linds\Documents\VS Code\GeoTest";
            string fname = @"F:\Digdag\Retslig\Herred.shp";
            ConvertShapefileToGeojson(fname, Path.Combine(outputDir, Path.GetFileNameWithoutExtension(fname) + ".json"), 0.0001,3);
            ConvertShapefileToGeojson(fname, Path.Combine(outputDir, Path.GetFileNameWithoutExtension(fname) + ".json"), 1670, 19700, 50, 0.0001, 3);

        }
    }
}
