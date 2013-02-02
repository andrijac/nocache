using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;

namespace NoCacheUpdater
{
	/*
	 Known limitations:
	 * 1. Resource file names should be unique: when searching for excluded files, it will search only by file name.	 
	 * 2. All possible prefixes should be defined in web.config.
	 * 3. Writes back in file in UTF8.
	 */

	class Program
	{
		static void Main(string[] args)
		{
			Func<string, string[]> getConfigCollection = (string key) => ConfigurationManager.AppSettings[key].Split(',').Select(val => val.Trim()).ToArray();

			// validation
			if (args.Length == 0)
			{
				WL("Folder name not supplied. Pass through target folder.");
				ExitApp();
				return;
			}

			DirectoryInfo targetFolder = new DirectoryInfo(args[0]);

			// validation
			if (!targetFolder.Exists)
			{
				WL("Folder '" + args[0] + "' does not exist.");
				ExitApp();
				return;
			}

			// very importaint collection, put all possible prefixes here.
			//string[] possiblePrefexes = new string[] { "/", "\"" };
			string[] possiblePrefexes = getConfigCollection("prefix");

			// sufix to be added in file name
			//string appendingSufix = DateTime.Now.ToString("yyyyMMdd_HHmm");
			string appendingFormat = ConfigurationManager.AppSettings["appendingFormat"];
			string appendingSufix = DateTime.Now.ToString(appendingFormat);

			// key - original full file name, value - new full file name
			Dictionary<string, string> foundFiles = new Dictionary<string, string>();

			string[] fileTypes = getConfigCollection("fileTypes");
			string[] targetFileTypes = getConfigCollection("targetFileTypes");
			string[] excludedFiles = getConfigCollection("excludeFiles");

			// resource files, key - file type (extension), value - collection of FileInfo-s found.
			Dictionary<string, List<FileInfo>> fileTypesInfo = new Dictionary<string, List<FileInfo>>();
			List<FileInfo> targetFileTypesInfo = new List<FileInfo>();

			bool foundFileType = false;
			// search for all resource files
			foreach (string fileType in fileTypes)
			{
				fileTypesInfo.Add(fileType, targetFolder.GetFiles("*." + fileType, SearchOption.AllDirectories).ToList());
				if (fileTypesInfo[fileType].Count > 0)
				{
					foundFileType = true;

					// Exclude files defined in config.

					List<FileInfo> exclude = new List<FileInfo>();
					foreach (FileInfo fi in fileTypesInfo[fileType])
					{
						if (excludedFiles.Contains(fi.Name))
						{
							exclude.Add(fi);
						}
					}

					foreach (FileInfo fi in exclude)
					{
						fileTypesInfo[fileType].Remove(fi);
					}

					exclude.Clear();
				}
			}

			// validation
			if (!foundFileType)
			{
				WL("No resource files found.");
				ExitApp();
				return;
			}

			// search for all target files containing resources
			foreach (string targetFileType in targetFileTypes)
			{
				targetFileTypesInfo.AddRange(targetFolder.GetFiles("*." + targetFileType, SearchOption.AllDirectories));
			}

			// validation
			if (targetFileTypesInfo.Count == 0)
			{
				WL("No files are found that contain specified file type resorces.");
				ExitApp();
				return;
			}

			// go through each target file to search for resources
			foreach (FileInfo fileInfo in targetFileTypesInfo)
			{
				StringBuilder sb = new StringBuilder();

				bool resourceFoundOnTarget = false;
				string line;
				int counter = 0;
				System.IO.StreamReader file = new System.IO.StreamReader(fileInfo.FullName);

				// go through file line by line
				while ((line = file.ReadLine()) != null)
				{
					// first search only for file type (extension) in each line.
					foreach (string fileType in fileTypes)
					{
						if (line.Contains("." + fileType))
						{
							// if extension is found, search for each file in resource files collection
							foreach (FileInfo resourceFile in fileTypesInfo[fileType])
							{
								// also, when comparing search results, compare possible prefixes
								foreach (string prefixes in possiblePrefexes)
								{
									string resourceFileWithPrefix = prefixes + resourceFile.Name;

									// check if line contains file name with prefix
									if (line.Contains(resourceFileWithPrefix))
									{
										// create new file name
										string newName = string.Format("{0}_{1}{2}", Path.GetFileNameWithoutExtension(resourceFile.Name), appendingSufix, Path.GetExtension(resourceFile.Name));

										// replace new resource file name
										line = line.Replace(prefixes + resourceFile.Name, prefixes + newName);
										resourceFoundOnTarget = true;

										// if found collection doesn't contain resource file yet, add it
										if (!foundFiles.ContainsKey(resourceFile.FullName))
										{
											string newPath = Path.Combine(Path.GetDirectoryName(resourceFile.FullName), newName);
											foundFiles.Add(resourceFile.FullName, newPath);
										}

										break;
									}
								}
							}
						}
					}

					sb.AppendLine(line);
					counter++;
				}

				file.Close();

				if (resourceFoundOnTarget)
				{
					// write all text back into file
					File.WriteAllText(fileInfo.FullName, sb.ToString(), Encoding.UTF8);
					WL(string.Format("Updated: {0}", fileInfo.FullName));
				}
				sb.Clear();
			}

			EmptyLine();

			WL("Used prefix is: " + appendingSufix);

			EmptyLine();

			if (foundFiles.Count > 0)
			{
				WL("Resources found and renamed:");
				// rename found resource files
				foreach (string key in foundFiles.Keys)
				{
					File.Move(key, foundFiles[key]);
					WL(string.Format(Path.GetFileName(key)));
				}
			}

			EmptyLine();
			WL("DONE");
			ExitApp();
		}

		static void ExitApp()
		{
			if (System.Diagnostics.Debugger.IsAttached)
			{
				Console.ReadLine();
			}
		}

		static void WL(object value)
		{
			Console.WriteLine(value.ToString());
		}

		static void EmptyLine()
		{
			WL(string.Empty);
		}
	}
}
