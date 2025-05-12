# New Mod

A template for a Risk of Rain 2 mod. Updated for game version v1.3.6#371

There's now language file support, easy thunderstore build packaging, proper logging, an open-source license, and a tutorial.

*Disclaimer: I mainly use VSCode, Visual Studio is better suited for these projects but I like my themes.*

**Credits to [Goorakh](https://github.com/Goorakh) for most of the project structure.**

### Quickstart

1. On the GitHub repo, select the `Use this template` button then click `Create a new repository`. Or clone/download the repo.
2. Change the `NewMod.sln` and `NewMod.csproj` filenames to your mod's name
3. Inside of `NewMod.sln`, change any instance of "NewMod" to your mod's name `NewMod", "NewMod\NewMod.csproj`
4. Change the `NewMod` folder name to your mod's name
5. Inside of `LanguageFolderHandler.cs`, `Log.cs`, and `Main.cs` change the namespace from `NewMod` to your mod's name
6. Inside of the `Log.cs` file, find this code line `const string MOD_NAME = nameof(NewMod);` and replace `NewMod` with your mod's name
7. Inside of `Main.cs`, change the `PluginAuthor` to your Thunderstore team name (sign into thunderstore and create a team), change the `PluginName` to the mod name, and `PluginVersion` to whatever version you're launching.
8. Inside of `License.txt` change `Nuxlar` to whatever you go by online (don't dox yourself and upload it to GitHub please)
9. Fill in the `manifest.json` with your mod info, you can leave the `website_url` blank or add a link to the GitHub issues tab of your repo
10. Build the project
    - In Visual Studio, you just right click the solution or csproj and click `rebuild`
    - In VSCode, open a terminal at the root of your mod project and run `dotnet build --configuration Debug` or `dotnet build --configuration Release` if you're ready to publish to Thunderstore (you do need the dotnet sdk installed)

## Changelog

**1.0.0**

- Release
