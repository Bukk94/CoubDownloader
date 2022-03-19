# Coub Downloader

[![MIT License](https://img.shields.io/apm/l/atomic-design-ui.svg?)](https://github.com/tterb/atomic-design-ui/blob/master/LICENSEs)
![version](https://img.shields.io/badge/version-0.4-blue)

This downloader is targeted at Windows machines for all fans of [Coub](http://www.coub.com).
For now, this downloader is able to download: 
* **LIKED** coubs from user's profile (keyword `liked`)
* **Bookmarked** coubs (keyword `bookmarks`)
* Coubs from any channel

## How it works

![guide](CoubDownloader/Img/Example.gif)

User will input names of what to download. Then the tool it will gather 
all the links with Coubs metadata. In the second phase
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
  * Go to `\bin` folder and find `ffmpeg.exe`
  * Copy this executable file into root folder of the downloader (at the same level as CoubDownloader.exe)
* **OR**
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

## Understanding metadata
### Basic raw metadata
Metadata are JSON files containing detail information about each coub.
Within those metadata you can find information like Coub's category,
views/likes/dislikes count, tags, size, audio/video URLs with different qualities,
if coub is NSFW, banned, age restricted, cropped, and many many more.

### Segments
Special metadata type are segments. They
contain similar information like metadata. But in addition to that
they contain raw video (without COUB watermark) as well as exact audio
marks. 

Not every coub has segments data (mostly recoubs) and sometimes the 
segments might not be even generated. Segments generation can be tracked
by linked state/progress. I assume that generation is triggered by 
hitting correct API endpoint on /segments.

## How to find Access Token
There are several ways to obtaining your Access Token.

**Option 1:**
1. Log into your Coub account
2. Go to your [likes page](https://coub.com/likes)
3. Next the URL address, you'll find a small lock icon

   ![guide](CoubDownloader/Img/Guide_3.png)

4. After clicking on the lock, a small window appears. Select `Cookies`:

   ![guide](CoubDownloader/Img/Guide_4.png)

5. New window will pop out. Select `coub.com` and then `Cookies` folder:

   ![guide](CoubDownloader/Img/Guide_5.png)

6. In this list, find item called `remember_token`

   ![guide](CoubDownloader/Img/Guide_6.png)

7. Click on this value and it should display details
8. You should see long set of numbers and letters, this is your Access Token, 
 copy it to the tool when asked. 

**Option 2:**

1. Log into your Coub account
2. Go to your [likes page](https://coub.com/likes)
3. Open developer console (press F12 if using Google Chrome)
4. Go to Network tab (number 1 in picture)
5. Reload the page (refresh / press F5)
6. You should see a lot of stuff going on in the Network tab. Wait for page to fully load
7. To ease the search, type 'likes' into the search bar (number 2 in picture)
8. Click on the row called 'likes' (number 3 in picture)
9. In 'Response Headers', search for value `remember_token=`
10. The value after `=` is your Access Token, 
copy it to the tool when asked.

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
* **Problem**: Program closes right after opening
  * **Solution**: This is caused by missing [.NET 5.0 runtime library](https://dotnet.microsoft.com/en-us/download/dotnet/5.0/runtime). 
 Try installing the runtime or downloading self-contained version of the tool.