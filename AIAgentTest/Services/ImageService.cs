

namespace AIAgentTest.Services
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Drawing;
    using SixLabors.ImageSharp.Drawing.Processing;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Xml;
    using Color = SixLabors.ImageSharp.Color;
    using Image = SixLabors.ImageSharp.Image;
    using Path = System.IO.Path;
    using Rectangle = SixLabors.ImageSharp.Rectangle;
    using System.Text.RegularExpressions;

    public static class ImageService
    {
        public static void DisplayImageWithBox(string imagePath, Rectangle rectangle)
        {
            using (Image image = Image.Load(imagePath))
            {
                image.Mutate(ctx => ctx
                    .Draw(Color.Red, 2f, rectangle));

                // Save the modified image
                string outputPath = $"{Path.GetFileNameWithoutExtension(imagePath)}_with_box.png";
                image.Save(outputPath);

                Console.WriteLine($"Image with box saved to: {outputPath}");

                // Open the image with the default viewer
                OpenImageWithDefaultViewer(outputPath);
            }
        }


        public static void DisplayImageWithBox(string imagePath, string coordinates)
        {
            using (Image image = Image.Load(imagePath))
            {
                var rectangle = ParseCoordinates(coordinates, image.Width, image.Height);

                image.Mutate(ctx => ctx
                    .Draw(Color.Red, 2f, rectangle));

                // Save the modified image
                string outputPath = $"{Path.GetFileNameWithoutExtension(imagePath)}_with_box.png";
                image.Save(outputPath);

                Console.WriteLine($"Image with box saved to: {outputPath}");

                // Open the image with the default viewer
                OpenImageWithDefaultViewer(outputPath);
            }
        }


        public static string GetImageWithMolmoPoints(string imagePath, string molmoPointsXml)
        {
            using (Image image = Image.Load(imagePath))
            {
                var points = ParseMolmoPoints(molmoPointsXml, image.Width, image.Height);

                // Extract the point name (assuming it's the inner text of the <points> element)
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(molmoPointsXml);
                var node = xmlDoc.SelectSingleNode("//points") ?? xmlDoc.SelectSingleNode("//point");
                string pointName = node?.InnerText?.Trim() ?? "default";

                // Sanitize the point name for use in a file name
                pointName = Regex.Replace(pointName, @"[^\w\d\s-_]", "_");

                // Calculate the scaling factor based on the displayed size
                const float maxDisplayedSize = 200f; // Max display size (width or height)
                float scaleFactor = Math.Min(maxDisplayedSize / image.Width, maxDisplayedSize / image.Height);

                foreach (var point in points)
                {
                    // Define the base radius for a 200x200 image
                    float baseRadius = 2;

                    // Scale the radius dynamically based on the image resolution
                    float radius = baseRadius / scaleFactor;

                    // Create an ellipse (circle) shape for each point
                    var circle = new EllipsePolygon(point.X, point.Y, radius);

                    // Draw the circle on the image
                    image.Mutate(ctx => ctx.Fill(Color.Red, circle));
                }

                // Construct the output file path with the sanitized point name
                string outputFileName = $"{Path.GetFileNameWithoutExtension(imagePath)}_{pointName.Normalize().Replace(" ", "_")}_with_molmo_points.png";
                string outputPath = Path.Combine(Path.GetDirectoryName(imagePath), outputFileName);

                image.Save(outputPath);

                Console.WriteLine($"Image with Molmo points saved to: {outputPath}");
                return outputPath;
            }
        }


        public static List<Vector2> ParseMolmoPoints(string molmoPointsXml, int imageWidth, int imageHeight)
        {
            var points = new List<Vector2>();
            var xmlDoc = new XmlDocument();

            try
            {
                xmlDoc.LoadXml(molmoPointsXml);

                // Check for <points> (plural) node
                var pointsNode = xmlDoc.SelectSingleNode("//points");
                if (pointsNode != null && pointsNode.Attributes != null)
                {
                    foreach (XmlAttribute attribute in pointsNode.Attributes)
                    {
                        // Process attributes like x1, y1, x2, y2 dynamically
                        if (attribute.Name.StartsWith("x") && int.TryParse(attribute.Name.Substring(1), out int indexX))
                        {
                            string yAttrName = "y" + indexX;
                            var yAttribute = pointsNode.Attributes[yAttrName];

                            if (yAttribute != null &&
                                float.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float xValue) &&
                                float.TryParse(yAttribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float yValue))
                            {
                                float x = xValue * imageWidth / 100;
                                float y = yValue * imageHeight / 100;
                                points.Add(new Vector2(x, y));
                            }
                        }
                    }
                }
                else
                {
                    // Check for a single <point> (singular) node
                    var pointNode = xmlDoc.SelectSingleNode("//point");
                    if (pointNode != null &&
                        pointNode.Attributes["x"] != null &&
                        pointNode.Attributes["y"] != null)
                    {
                        if (float.TryParse(pointNode.Attributes["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float xValue) &&
                            float.TryParse(pointNode.Attributes["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float yValue))
                        {
                            float x = xValue * imageWidth / 100;
                            float y = yValue * imageHeight / 100;
                            points.Add(new Vector2(x, y));
                        }
                    }
                }
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"Error parsing XML: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }

            return points;
        }

        public static Vector2 GetImageSize(string imagePath)
        {
            using (Image image = Image.Load(imagePath))
            {
                return new Vector2(image.Width, image.Height);
            }
        }

        public static Rectangle ParseCoordinates(string coordinates, int imageWidth, int imageHeight)
        {
            // Try to match the normalized coordinate format [x1, y1, x2, y2]
            var normalizedMatch = Regex.Match(coordinates, @"\[(\d*[.,]?\d+),\s*(\d*[.,]?\d+),\s*(\d*[.,]?\d+),\s*(\d*[.,]?\d+)\]");
            if (normalizedMatch.Success)
            {
                float x1 = ParseFloat(normalizedMatch.Groups[1].Value);
                float y1 = ParseFloat(normalizedMatch.Groups[2].Value);
                float x2 = ParseFloat(normalizedMatch.Groups[3].Value);
                float y2 = ParseFloat(normalizedMatch.Groups[4].Value);
                return NormalizedToPixelCoordinates(x1, y1, x2, y2, imageWidth, imageHeight);
            }

            // Try to match the <box> format
            var boxMatch = Regex.Match(coordinates, @"<box>(\d+)\s+(\d+)\s+(\d+)\s+(\d+)</box>");
            if (boxMatch.Success)
            {
                int x = int.Parse(boxMatch.Groups[1].Value);
                int y = int.Parse(boxMatch.Groups[2].Value);
                int width = int.Parse(boxMatch.Groups[3].Value) - x;
                int height = int.Parse(boxMatch.Groups[4].Value) - y;
                return new Rectangle(x, y, width, height);
            }

            // Try to match the <rect> format
            var rectMatch = Regex.Match(coordinates, @"<rect x=""(\d+)"" y=""(\d+)"" width=""(\d+)"" height=""(\d+)""/>");
            if (rectMatch.Success)
            {
                int x = int.Parse(rectMatch.Groups[1].Value);
                int y = int.Parse(rectMatch.Groups[2].Value);
                int width = int.Parse(rectMatch.Groups[3].Value);
                int height = int.Parse(rectMatch.Groups[4].Value);
                return new Rectangle(x, y, width, height);
            }

            // Try to match the format with top-left and bottom-right coordinates (both variants)
            var cornerMatch = Regex.Match(coordinates, @"(?:Top-left(?:\s+corner)?:?\s*\((\d+),\s*(\d+)\).*Bottom-right(?:\s+corner)?:?\s*\((\d+),\s*(\d+)\))|(?:Top-left(?:\s+corner)?:?\s*\((\d+),\s*(\d+)\).*Bottom-right(?:\s+corner)?:?\s*\((\d+),\s*(\d+)\))", RegexOptions.Singleline);
            if (cornerMatch.Success)
            {
                int x1 = int.Parse(cornerMatch.Groups[1].Success ? cornerMatch.Groups[1].Value : cornerMatch.Groups[5].Value);
                int y1 = int.Parse(cornerMatch.Groups[2].Success ? cornerMatch.Groups[2].Value : cornerMatch.Groups[6].Value);
                int x2 = int.Parse(cornerMatch.Groups[3].Success ? cornerMatch.Groups[3].Value : cornerMatch.Groups[7].Value);
                int y2 = int.Parse(cornerMatch.Groups[4].Success ? cornerMatch.Groups[4].Value : cornerMatch.Groups[8].Value);
                return new Rectangle(x1, y1, x2 - x1, y2 - y1);
            }

            // Try to match the concise format "(x1, y1) to (x2, y2)", "[x1, y1] to [x2, y2]", or "x1, y1 to x2, y2"
            var conciseMatch = Regex.Match(coordinates, @"(?:[\(\[]?(\d+),?\s*(\d+)[\)\]]?)?\s*to\s*(?:[\(\[]?(\d+),?\s*(\d+)[\)\]]?)?");
            if (conciseMatch.Success && conciseMatch.Groups.Count == 5 && conciseMatch.Groups.Cast<Group>().All(g => g.Success))
            {
                int x1 = int.Parse(conciseMatch.Groups[1].Value);
                int y1 = int.Parse(conciseMatch.Groups[2].Value);
                int x2 = int.Parse(conciseMatch.Groups[3].Value);
                int y2 = int.Parse(conciseMatch.Groups[4].Value);
                return new Rectangle(x1, y1, x2 - x1, y2 - y1);
            }

            throw new ArgumentException("Invalid coordinates format");
        }

        public static Rectangle NormalizedToPixelCoordinates(float x1, float y1, float x2, float y2, int imageWidth, int imageHeight)
        {
            int pixelX1 = (int)(x1 * imageWidth);
            int pixelY1 = (int)(y1 * imageHeight);
            int pixelX2 = (int)(x2 * imageWidth);
            int pixelY2 = (int)(y2 * imageHeight);

            return new Rectangle(pixelX1, pixelY1, pixelX2 - pixelX1, pixelY2 - pixelY1);
        }

        private static float ParseFloat(string value)
        {
            // Try parsing with invariant culture (dot as decimal separator)
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }

            // If that fails, try parsing with comma as decimal separator
            if (float.TryParse(value.Replace('.', ','), NumberStyles.Float, CultureInfo.GetCultureInfo("da-DK"), out result))
            {
                return result;
            }

            throw new FormatException($"Unable to parse float value: {value}");
        }

        public static void OpenImageWithDefaultViewer(string imagePath)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(imagePath) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", imagePath);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", imagePath);
                }
                else
                {
                    Console.WriteLine("Unsupported operating system. Please open the image manually.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while trying to open the image: {ex.Message}");
            }
        }
    }
}
