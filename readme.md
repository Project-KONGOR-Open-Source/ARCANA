<h3>
    <p align="center">ARCANA</p>
    <p>A collection of miscellaneous tools for automating various development-adjacent tasks.</p>
</h3>

<hr/>

<h3 align="center">Tool Descriptions</h3>

- Archive
    - **ConsolidateVersions** : Used to create a distribution from an archive of incremental distribution versions. This tool incrementally applies versions on top of each other, deleting each version directory after it has been processed. The final result is a hierarchical structure of ZIP files representing the latest respective version of each distribution file. This final result would be the equivalent of a distribution's CDN backup.

- Distribution
    - **CheckFileSizes** : Used to assert the integrity of a distribution's CDN backup. This tool checks the respective size of each ZIP file included in the distribution, following the tree structure defined by the distribution's manifest.
    - **UnpackFiles** : Used to create a clean distribution. This tool unpacks each ZIP file included in the distribution, following the tree structure defined by the distribution's manifest. Once a file has been unpacked, the original ZIP file is deleted. Lastly, unpacked resource files will be bundled together into S2Z files.

> [!NOTE]
> Terminology
>    - archive : the collection of a distribution's incremental versions; this will be composed of multiple directories along the lines of `4.8.6`, `...`, `4.10.1`
>    - distribution : the collection of files that make up a client or a server for a particular platform; a distribution's name is a 3-letter code, sometimes followed by a secret key to mask the files over the CDN; distribution name examples include `wac` and `las`
>        - the first letter represents the distribution's platform, and is one of `w` for Windows, `l` for Linux, or `m` for macOS
>        - the second letter represents the distribution's target, and is one of `a` for International, `g` for Garena, `r` for RCT, or `t` for SBT
>        - the third letter represents the distribution's type, and is either `c` for client, or `s` for server

The workflow for the tools above is the following:
  1. produce a CDN backup by either downloading the individual files from the CDN (no longer possible) or by consolidating the versions of a distribution archive (if you have one laying around)
  2. check the file sizes against the manifest to confirm the distribution's integrity
  3. unpack the distribution's files and create the S2Z resource bundles
  4. we should now have a clean client or server distribution, ready to be run
