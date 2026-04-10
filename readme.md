<h3>
    <p align="center">ARCANA</p>
    <p>A collection of miscellaneous tools for automating various development-adjacent tasks.</p>
</h3>

<hr/>

<h3 align="center">Tool Descriptions</h3>

- Archive
    - **ConsolidateVersions** : Used to create a distribution from an archive of incremental distribution versions. This tool incrementally applies versions on top of each other, deleting each version directory after it has been processed. The final result is a hierarchical structure of ZIP files representing the latest respective version of each distribution file. This final result would be the equivalent of a distribution's CDN backup.
    - **CheckFileSizes** : Used to assert the integrity of a distribution's CDN backup. This tool checks the respective size of each ZIP file included in the distribution against the sizes recorded in the distribution's manifest.
    - **UnpackFiles** : Used to create a clean distribution from a consolidated archive. This tool unpacks each ZIP file included in the distribution, following the tree structure defined by the distribution's manifest. Once a file has been unpacked, the original ZIP file is deleted. Optionally, unpacked resource directories can be bundled together into S2Z files.

- Distribution
    - **PackResourceFiles** : Used to pack directories with an `.s2z` suffix back into S2Z archives (ZIP format). Each source directory is deleted after the archive is successfully created. Failed operations are retried with exponential backoff.
    - **UnpackResourceFiles** : Used to unpack S2Z resource archives (ZIP format) into directories with an `.s2z` suffix. Each archive is deleted after successful extraction. Failed operations are retried with exponential backoff.
    - **UploadToObjectStorage** : Used to upload all files from a local directory to an S3-compatible object storage bucket such as Cloudflare R2 or AWS S3. Tracks total progress as file count and percentage, retries failed uploads with exponential backoff, and logs failures to the console.
    - **DownloadFromObjectStorage** : Used to download all objects under a given key prefix from an S3-compatible object storage bucket such as Cloudflare R2 or AWS S3 to a local directory. Preserves the relative directory structure, tracks total progress as file count and percentage, retries failed downloads with exponential backoff, and logs failures to the console.
    - **DeleteFromObjectStorage** : Used to delete all objects under a given key prefix from an S3-compatible object storage bucket such as Cloudflare R2 or AWS S3. Tracks total progress as object count and percentage, retries failed deletions with exponential backoff, and logs failures to the console.

<br/>

> [!NOTE]
> Terminology
>    - archive : the collection of a distribution's incremental versions; this will be composed of multiple directories along the lines of `4.8.6`, `...`, `4.10.1`
>    - distribution : the collection of files that make up a client or a server for a particular platform; a distribution's name is a 3-letter code, sometimes followed by a secret key to mask the files over the CDN; distribution name examples include `wac` and `las`
>        - the first letter represents the distribution's platform, and is one of `w` for Windows, `l` for Linux, or `m` for macOS
>        - the second letter represents the distribution's target, and is one of `a` for International, `g` for Garena, `r` for RCT, or `t` for SBT
>        - the third letter represents the distribution's type, and is either `c` for client, or `s` for server

<br/>

The workflow for the tools above is the following:
  1. produce a CDN backup by either downloading the individual files from the CDN (no longer possible) or by consolidating the versions of a distribution archive (if you have one laying around)
  2. check the file sizes against the manifest to confirm the distribution's integrity
  3. unpack the archive's ZIP files and, optionally, bundle the S2Z resource directories
  4. we should now have a clean client or server distribution, ready to be run
  5. optionally, unpack S2Z resource archives for inspection or modding, and pack them back when done
  6. optionally, upload the distribution's files to S3-compatible object storage
  7. optionally, delete objects from S3-compatible object storage when they are no longer needed

<hr/>

<h3 align="center">Command Examples</h3>

**Archive.ConsolidateVersions**

```
./Archive.ConsolidateVersions.exe "path/to/directory"
```

If no directory is provided, the current working directory is used.

**Archive.CheckFileSizes**

```
./Archive.CheckFileSizes.exe "path/to/directory"
```

If no directory is provided, the current working directory is used. The directory must contain a `manifest.xml.zip` file.

**Archive.UnpackFiles**

```
./Archive.UnpackFiles.exe "path/to/directory" false
```

The first argument is the directory containing the ZIP files. The second argument (`true`/`false`) controls whether resource files are bundled into S2Z files. If omitted, resource files are not bundled.

**Distribution.PackResourceFiles**

```
./Distribution.PackResourceFiles.exe "path/to/directory"
```

If no directory is provided, the current working directory is used. Optional 2nd argument: maximum retry attempts per directory (default: `5`).

**Distribution.UnpackResourceFiles**

```
./Distribution.UnpackResourceFiles.exe "path/to/directory"
```

If no directory is provided, the current working directory is used. Optional 2nd argument: maximum retry attempts per archive (default: `5`).

**Distribution.UploadToObjectStorage**

```
./Distribution.UploadToObjectStorage.exe "path/to/directory" "bucket-name" "https://ACCOUNT_ID.r2.cloudflarestorage.com" "ACCESS_KEY_ID" "SECRET_ACCESS_KEY"
```

The first five arguments are required. Two optional arguments can follow:
- 6th argument: maximum retry attempts per file (default: `5`)
- 7th argument: key prefix to prepend to all uploaded object keys (e.g. `wac/4.10.1`, to organize files under a subdirectory in the bucket)

**Distribution.DownloadFromObjectStorage**

```
./Distribution.DownloadFromObjectStorage.exe "path/to/directory" "bucket-name" "https://ACCOUNT_ID.r2.cloudflarestorage.com" "ACCESS_KEY_ID" "SECRET_ACCESS_KEY" "wac/4.10.1"
```

The first six arguments are required. Optional 7th argument: maximum retry attempts per file (default: `5`).

**Distribution.DeleteFromObjectStorage**

```
./Distribution.DeleteFromObjectStorage.exe "bucket-name" "https://ACCOUNT_ID.r2.cloudflarestorage.com" "ACCESS_KEY_ID" "SECRET_ACCESS_KEY" "wac/4.10.1"
```

The first five arguments are required. Optional 6th argument: maximum retry attempts per deletion batch (default: `5`).
