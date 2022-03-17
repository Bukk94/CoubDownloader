# Coub Downloader

[![MIT License](https://img.shields.io/apm/l/atomic-design-ui.svg?)](https://github.com/tterb/atomic-design-ui/blob/master/LICENSEs)
![version](https://img.shields.io/badge/version-0.2-blue)

This downloader is targeted at Windows machines for all fans of [Coub](http://www.coub.com).
For now, this downloader is able to download: 
* **LIKED** coubs from user's profile (keyword `liked`)
* **Bookmarked** coubs (keyword `bookmarks`)
* Coubs from any channel

## How it works

User will input names of what to download. Then the tool it will gather 
all the links with some meta data. In the second phase
it will download all gathered coubs one by one. Each coub will be processed
in highest available quality in mp4 format.

If user is downloading liked/bookmarked coubs, he must provide personal Access Token.

Tool will automatically skip URL gathering if URL list already exists and 
skips all already downloaded coubs (if name matches).

You can also choose to download your own list by inserting it
into proper structure. Filename must be `url_list.txt` and 
URLs must be separated by new-lines. When running the downloader,
during input insert nothing (just hit enter to continue).

## Requirements
* ffmpeg
* Python 3.6 and above (included in release package)

## How to install ffmpeg

* Download newest version of [ffmpeg here](https://www.gyan.dev/ffmpeg/builds/ffmpeg-git-full.7z)
* Unzip downloaded file, for example to `C:\ffmpeg`
* Open CMD with admin privileges and type following command:
* `setx /m PATH "C:\ffmpeg\bin;%PATH%"`
* Restart PC (yes, this is mandatory)

If you save ffmpeg into different folder than `C:\ffmpeg`, don't forget to 
modify the command in CMD accordingly to match actual `\bin` directory.

## How to run
* Download latest release package (already contain portable Python)
* Make sure you have ffmpeg installed
* Run `CoubDownloader.exe`
* Enter channels to download, `liked` or `bookmarks` to download liked/bookmarked coubs.
  * You can download multiple channels at once, separated by comma (e.g. `liked,bookmarks,coub.channel`)

## Files structure
* [Root]\Coubs-info\\[dir]
  * Contains URLs to download as well as some metadata information like coub's
    original name and tags. Each category has its own folder
* [Root]\Coubs\\[dir]
  * Actual coubs downloaded by the tool

## How to find Access Token
1. Log into your Coub account
2. Go to your [likes page](https://coub.com/likes).
3. Open developer console (press F12 if using Google Chrome)
4. Go to Network tab (number 1 in picture)
5. Reload the page (refresh / press F5)
6. You should see a lot of stuff going on in the Network tab. Wait for page to fully load
7. To ease the search, type 'likes' into the search bar (number 2 in picture)
8. Click on the row called 'likes' (number 3 in picture)
9. In 'Response Headers', search for value `remember_token=`
10. The value after `=` is your Access Token, copy in to this app

![guide](CoubDownloader/Img/Guide_1.png)
![guide_2](CoubDownloader/Img/Guide_2.png)

## Credits

Downloader in python was written by [artemtar](https://github.com/artemtar/CoubDownloader).
I forked his repo and made several adjustments to avoid common formatting issues
and other minor problems.

## Known problems
* Some titles are so much crazy that they will appear in root directory with
just plain coub ID
* Error in the download process will crash whole program

## Troubleshooting
* Common problem is that tool is immediately closed after running. 
 This might be caused by missing .NET 5.0 library. 
 Try downloading self-contained version of the tool or install missing library.