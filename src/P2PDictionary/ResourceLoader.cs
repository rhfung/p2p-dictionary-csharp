using System;
using System.IO;
using System.Reflection;

namespace com.rhfung.P2PDictionary
{
    class ResourceLoader
    {
		private static string indexFile = null;
		private static string errorFile = null;

		public static string GetIndexFile() {
			if (indexFile != null) {
				return indexFile;
			}

			var assembly = Assembly.GetExecutingAssembly();
			String resourceName = @"com.rhfung.P2PDictionary.resources.index.html";
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new Exception($"Resource {resourceName} not found in {assembly.FullName}.  Valid resources are: {String.Join(", ", assembly.GetManifestResourceNames())}.");
                }
                using (var reader = new StreamReader(stream))
                {
					indexFile = reader.ReadToEnd();
					return indexFile;
                }
            }
		}

		public static string GetErrorFile()
        {
			if (errorFile != null) {
				return errorFile;
			}

            var assembly = Assembly.GetExecutingAssembly();
			String resourceName = @"com.rhfung.P2PDictionary.resources.error.html";
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new Exception($"Resource {resourceName} not found in {assembly.FullName}.  Valid resources are: {String.Join(", ", assembly.GetManifestResourceNames())}.");
                }
                using (var reader = new StreamReader(stream))
                {
					errorFile = reader.ReadToEnd();
					return errorFile;
                }
            }
        }
    }
}
