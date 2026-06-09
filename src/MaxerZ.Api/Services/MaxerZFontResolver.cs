using System;
using System.IO;
using System.Reflection;
using PdfSharpCore.Fonts;

namespace MaxerZ.Api.Services
{
    public class MaxerZFontResolver : IFontResolver
    {
        public string DefaultFontName => "OpenSans-Regular";

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Map everything to our embedded OpenSans font family
            if (isBold)
            {
                return new FontResolverInfo("OpenSans-Semibold");
            }
            return new FontResolverInfo("OpenSans-Regular");
        }

        public byte[] GetFont(string faceName)
        {
            var assembly = typeof(MaxerZFontResolver).Assembly;
            
            // Resource name format: <AssemblyName>.<Subfolders>.<Filename>
            var resourceName = faceName == "OpenSans-Semibold"
                ? "MaxerZ.Api.Resources.Fonts.OpenSans-Semibold.ttf"
                : "MaxerZ.Api.Resources.Fonts.OpenSans-Regular.ttf";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                // Fallback to check if we can load from system folder (Arial)
                string fallbackSystemPath = faceName == "OpenSans-Semibold"
                    ? "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
                    : "/System/Library/Fonts/Supplemental/Arial.ttf";

                if (File.Exists(fallbackSystemPath))
                {
                    try
                    {
                        return File.ReadAllBytes(fallbackSystemPath);
                    }
                    catch { }
                }

                throw new FileNotFoundException($"Font resource '{resourceName}' not found in assembly or system fallback.");
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
