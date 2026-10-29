# Coub Downloader

<p align="center">
    <a href="https://www.microsoft.com/net"><img src="https://img.shields.io/badge/-.NET%2010.0-blueviolet" style="max-height: 300px;"></a>
    <img src="https://img.shields.io/badge/version-2.0.0-blue" style="max-height: 300px;">
    <a href="https://github.com/Bukk94/CoubDownloader/blob/master/LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" style="max-height: 300px;"></a>
    <a href="https://github.com/Bukk94/CoubDownloader/releases"><img src="https://img.shields.io/github/downloads/Bukk94/CoubDownloader/total" style="max-height: 300px;"></a>
</p>

<p align="center">
  <a href='https://ko-fi.com/bukk94' target='_blank'>
  <img height='30' style='border:0;height:38px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' />
  </a>
</p>

Coub Downloader is a console application for Windows that downloads coubs from [Coub](https://coub.com)
in the best available quality, merged into a single mp4 file.

It can download:

* **Liked** coubs from your profile (keyword `liked`)
* **Bookmarked** coubs (keyword `bookmarks`)
* **Coubs** from any channel
* Channel's **reposts**
* **Your own list** of coub URLs

## Quick start

1. Download the `full` package from [Releases](https://github.com/Bukk94/CoubDownloader/releases) (it contains everything needed).
2. Run `CoubDownloader.exe`.
3. Type what you want to download, for example `liked,bookmarks`, and press Enter.

> [!NOTE]
> Liked and bookmarked coubs need your personal Auth Token, see [How to find Auth Token](#how-to-find-auth-token).
> Channels and your own lists work without it.

### Table of contents

* [How it works](#how-it-works)
    + [Downloading own list of URLs](#downloading-own-list-of-urls)
* [Requirements](#requirements)
* [How to install ffmpeg](#how-to-install-ffmpeg)
* [How to run](#how-to-run)
    + [Username vs Display name](#username-vs-display-name)
* [How to find Auth Token](#how-to-find-auth-token)
* [Configuration](#configuration)
    + [Configuration Properties](#configuration-properties)
* [Files structure](#files-structure)
* [Understanding metadata](#understanding-metadata)
* [Credit](#credit)
* [Troubleshooting](#troubleshooting)

## How it works

![guide](CoubDownloader/Img/Example.gif)

You enter the names of what you want to download. The tool then works in two phases:

1. **Gathering** - collects the links and metadata of all coubs.
2. **Downloading** - downloads the coubs one by one, each in the highest available quality (configurable)
   as an mp4 file.

If a channel (or `liked` / `bookmarks`) was already gathered, the tool asks whether to gather it again to get the
newest coubs. The old list is replaced only after the new one is successfully gathered. Coubs that were already
downloaded (listed in `downloaded.txt`, or whose file already exists) are always skipped.

### Downloading own list of URLs

You can download your own coubs by entering their URLs, or a text file with a list of URLs.

**Option 1: Enter the URLs directly**

Paste coub URLs into the input, separated by a comma:

```
https://coub.com/view/2k8xp4,https://coub.com/view/5qw7nd
```

**Option 2: Use a text file**

1. Create a text file anywhere you like (e.g. `mylist.txt`) with one coub URL per line:

   ```
   https://coub.com/view/1cmdn7
   https://coub.com/view/2zgvin
   ```

2. Run the downloader and enter the path to the file (e.g. `C:\coubs\mylist.txt`).
   You can also drag the file into the console window to paste its path.

Both options can be combined with channels and keywords, e.g. `liked,mylist.txt,https://coub.com/view/1cmdn7`.

Coubs from a text file are downloaded into a folder named after the file (`Coubs\mylist`),
coubs entered directly are downloaded into `Coubs\custom`.

> [!TIP]
> Before downloading, the tool gathers information about each URL, so the first run takes a bit longer
> (`WaitTime` is applied between coubs). Lines that are not coub links are skipped with a warning.
> The text file is read again on every run, so you can add or remove links and only the new ones are processed.

## Requirements

No installation or additional download is needed if you download the `full`
version from the [Releases](https://github.com/Bukk94/CoubDownloader/releases) section.

Otherwise you need:

* ffmpeg (see [How to install ffmpeg](#how-to-install-ffmpeg))
* [.NET 10.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0/runtime)

## How to install ffmpeg

ffmpeg is used to merge the video and audio of each coub into the final file. Choose one option:

**Option 1: Put it next to the downloader (simplest)**

1. Download [ffmpeg](https://www.gyan.dev/ffmpeg/builds/ffmpeg-git-full.7z) and unpack it (for example with 7-Zip).
2. Find `ffmpeg.exe` in the `bin` folder.
3. Copy `ffmpeg.exe` into the folder with `CoubDownloader.exe`.

**Option 2: Install it with winget**

Open a terminal and run:

```shell
winget install Gyan.FFmpeg
```

Then open a new terminal window (or sign out and in) so the updated `PATH` is picked up.

**Option 3: Add your own copy to PATH**

Unpack ffmpeg (e.g. to `C:\ffmpeg`) and add `C:\ffmpeg\bin` to your `PATH` through
Windows Settings > System > About > Advanced system settings > Environment Variables.
Then open a new terminal window.

> [!TIP]
> To check that ffmpeg is available, open a new terminal and run `ffmpeg -version`.

## How to run

1. Download the [latest release package](https://github.com/Bukk94/CoubDownloader/releases).
2. Make sure you have [ffmpeg](#how-to-install-ffmpeg) available.
3. Run `CoubDownloader.exe`.
4. Enter what to download:
   * channel **usernames** (not display names!) or channel URLs,
   * `liked` or `bookmarks` for your own coubs (requires your [Auth Token](#how-to-find-auth-token)),
   * coub URLs or a text file with URLs, see [Downloading own list of URLs](#downloading-own-list-of-urls).

   You can combine multiple entries separated by a comma, e.g. `liked,bookmarks,coub.channel`.
   Leave the input empty to download everything that was already gathered.

### Username vs Display name

To download a specific channel, you must enter the channel's username.
This can be confusing, as the username is not the same thing as the display name.
My display name is `Bukk94`, but my username is `bukk9433113`.

The username is often auto-generated (unless specified otherwise) and consists of
8 or more characters, numbers, dots or dashes. You can find it as part of the
channel's URL address (e.g. `https://coub.com/bukk9433113`).

## How to find Auth Token

The Auth Token is required for downloading coubs related to your account (liked / bookmarked coubs).
Coub moved to a new login system, so the old `remember_token` no longer works.
The token is the value of the `sb-gotrue-auth-token` cookie (it starts with `base64-`).

1. Log into your Coub account.
2. Go to any Coub page (e.g. [likes page](https://coub.com/likes)).
3. Right click on the page and select `Inspect` (or press F12 to open the developer console).
4. Go to the `Application` tab - if you don't see this tab, click the arrows.
5. On the left side, find `Cookies` and click on `https://coub.com`.
6. In the list, find the item called `sb-gotrue-auth-token` - its value starts with `base64-` and is very long.
7. Click on this item and copy the value from the details at the bottom of the screen,
   then paste it into the tool when asked.

![guide](CoubDownloader/Img/Guide_Cookies.png)

> [!NOTE]
> The token is valid for about one hour. If it expires during a long download, CoubDownloader automatically
> refreshes it (the same way your browser does) and continues. The new token is kept only until the tool is closed.

> [!WARNING]
> A refresh uses up the old token, so it can sign you out of the browser session the token was copied from.
> Treat the token like a password and never share it.

## Configuration

Coub Downloader is meant to be as simple as possible. Although you can set
some settings through the downloader itself, it wasn't enough for more advanced users.
For those users, there is the `Configuration.json` file in the root directory.
You can open it with any text editor (for example `notepad`) and further modify the final
output of the downloader.

### Configuration properties

| Property           | Default   | Allowed values                                 | Description                                                                                                                                                                                                                    |
|--------------------|-----------|------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Loops`            | `-1`      | `-1` or any positive number                    | Number of loops of the final video. By default the video loops until the audio ends, set `1` for just one loop.                                                                                                                |
| `WaitTime`         | `2.5`     | any positive number                            | Seconds to wait between requests and downloads. This reduces Coub's server load and helps you avoid ban detection.                                                                                                             |
| `VideoQuality`     | `Highest` | `Highest`, `Medium`, `Low`                     | Highest is the best available quality, usually 720p to 1080p. Medium is standard quality (~360p). Low is the lowest available quality.                                                                                         |
| `Ordering`         | `Newest`  | `Newest`, `Oldest`, `LikesCount`, `ViewsCount` | Order in which coubs are downloaded.                                                                                                                                                                                           |
| `KeepAudioVideo`   | `false`   | `true`, `false`                                | Each coub consists of audio and video, which are combined into the final file. By default the separate files are removed, set `true` to keep them.                                                                             |
| `OutputFolderPath` | `""`      | full folder path                               | By default all coubs are downloaded next to `CoubDownloader.exe`. Set your own folder (e.g. on a different drive). The path must end with a backslash and each backslash must be doubled, e.g. `D:\\Downloads\\MyCoubs\\`.     |
| `NsfwOnly`         | `false`   | `true`, `false`                                | If CoubDownloader should download only NSFW coubs or all of them. NOTE that Coub will permanently remove all NSFW coubs on **June 27th 2022**, and this option will stop working (as there will be nothing to download).       |
| `ProxyUrl`         | `null`    | `http://` or `https://` URL                    | HTTP/HTTPS proxy to route all requests through (e.g. `http://proxy.example.com:8080` or `http://user:password@proxy.example.com:8080`). Useful if your IP is being blocked by Coub. Applies to both gathering and downloading. |

## Files structure

```
[Root]
|-- Coubs-info
|   |-- downloaded.txt            list of already downloaded coubs
|   `-- [dir]
|       |-- url_list.txt          coub URLs to download
|       |-- url_list_reposts.txt  reposted coub URLs (channels only)
|       |-- media.json            media links used by the downloader
|       |-- metadata.txt          readable summary of each coub
|       `-- raw_metadata.json     all data Coub returned for each coub
`-- Coubs
    `-- [dir]
        |-- ...                   downloaded coubs
        `-- Reposts               channel's reposted coubs
```

Each category (a channel, `liked`, `bookmarks` or your own list) has its own `[dir]` folder. For your own lists
it is the name of the text file (e.g. `mylist`), or `custom` for URLs entered directly.
Downloaded coubs are named `<coub id>_<title>.mp4`.

## Understanding metadata

When gathering coubs, the tool saves the information Coub provides about each of them
into the category's folder in `Coubs-info`:

* `metadata.txt` - a readable summary: URL, title, tags, creation date, duration, NSFW flag and the author.
* `raw_metadata.json` - everything Coub returns for each coub, one JSON object per coub. This includes
  the community, view/like/repost counters, tags, publish date, duration, age restriction flag, sources
  and direct links to the video and audio in different qualities.
* `media.json` - only the links the downloader needs (video in different qualities and audio).
  If you delete it, it is created again from `url_list.txt` the next time you run the tool.

## Credit

The original download script was written by [artemtar](https://github.com/artemtar/CoubDownloader).
It was used as a base for this tool until Coub changed its API.

## Troubleshooting

<details>
<summary><b>Program closes right after opening</b></summary>

This is probably caused by a missing [.NET 10.0 runtime library](https://dotnet.microsoft.com/en-us/download/dotnet/10.0/runtime).
Try installing the runtime library or downloading the `full` version of the tool.
</details>

<details>
<summary><b>CoubDownloader cannot download coubs/lists and I can't access Coub websites anymore</b></summary>

That means you were temporarily IP banned from Coub's website (usually for 1 hour).
Don't panic, after some time you'll be able to access it again.
This happens when you are mass downloading coubs within a short interval.
Try opening the `Configuration.json` file and increasing `WaitTime` to 3 or more seconds.
If blocks persist, you can also set `ProxyUrl` in `Configuration.json` to route traffic through a different IP address.
</details>
