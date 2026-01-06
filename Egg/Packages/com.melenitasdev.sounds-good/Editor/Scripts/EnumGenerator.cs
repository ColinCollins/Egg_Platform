/*
 * All rights to the Sounds Good plugin, © Created by Melenitas Dev, are reserved.
 * Distribution of the standalone asset is strictly prohibited.
 */
#if UNITY_EDITOR
using System;
using System.IO;

namespace MelenitasDev.SoundsGood.Editor
{
    internal class EnumGenerator : IDisposable
    {
        internal void GeneratePseudoEnum (string enumName, string[] tags, string locationPath)
        {
            string ident = "\t";
            using (StreamWriter streamWriter = new StreamWriter(locationPath))
            {
                streamWriter.WriteLine("namespace MelenitasDev.SoundsGood");
                streamWriter.WriteLine("{");
                streamWriter.WriteLine(ident + "public partial struct " + enumName);
                streamWriter.WriteLine(ident + "{");

                if (tags is { Length: > 0 })
                {
                    foreach (var tag in tags)
                    {
                        streamWriter.WriteLine(
                            ident + ident +
                            "public static readonly " + enumName + " " + tag +
                            " = new " + enumName + "(\"" + tag + "\");"
                        );
                    }
                }

                streamWriter.WriteLine(ident + "}");
                streamWriter.WriteLine("}");
            }
        }

        public void Dispose ()
        {
            
        }
    }
}
#endif