YamlDotNet 16.3.0 (netstandard2.1 build), from https://www.nuget.org/packages/YamlDotNet/16.3.0
Source: https://github.com/aaubry/YamlDotNet
License: MIT. Copyright (c) Antoine Aubry and contributors.

Used by Hearthline.Sim to parse the YAML in Content/. The headless build in tools/headless references
this same file, so Unity and the headless tests always run the same parser. To upgrade, replace the DLL
and update the version above.
