# UnitypackgeExtractor
### A simple program to help you extract your unitypackages without opening unity.

## Usage
1. Download the latest release from the releases page.
2. Extract the zip to a known loacation.
3. Open the exe or drop a unitypackage onto the exe.
4. Follow the prompts.
5. The package will be extracted alongside the unitypackage.

## Advanced usage
You can also use the command line to extract unitypackages with some options.

Example: extract.exe [unitypackage] [-options]
* "-noMeta" : Skip the extraction of unity's .meta files. (by default the meta files is necessary)
* "-outputPreview" : Enable extraction of unity's preview files. (by default the preview is omitted)
* "-wait" : Wait in the end and ask user to press a key to finish. (by default the app will exit by itself)

Use multi-threaded to unpack a unitypackage. The thread number is the same as core number.