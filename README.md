# Coub Downloader

This downloader is targeted at Windows machines for all fans of [Coub](http://www.coub.com).
For now, this downloader is able to download **LIKED** coubs from user's profile
by provided access token.

## How it works

First user will enter his Access Token, so the crawler can see liked coubs.
Then it will gather all the links with some meta data. In the last phase
it will download all gathered coubs one by one. Each coub will be processed
in highest available quality in mp4 format.

## Requirements
* Python 3.6 and above
* ffmpeg

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

![guide](Img/Guide_1.png)
![guide_2](Img/Guide_2.png)

## How to install ffmpeg

* Download newest version of [ffmpeg here](https://www.gyan.dev/ffmpeg/builds/ffmpeg-git-full.7z)
* Unzip downloaded file, for example to `C:\ffmpeg`
* Open CMD with admin privileges and type following command:
* `setx /m PATH "C:\ffmpeg\bin;%PATH%"`
* Restart PC (yes, this is mandatory)

If you change the path from C:\ffmpeg, modify the command accordingly

## How to run
* Download latest release package (should already contain Python)
* Make sure you have ffmpeg installed
* Run CoubDownloader.exe

## Files structure
* [Root]\Coubs-info
  * Contains URLs to download as well as some metadata information like coub's 
  original name and tags
* [Root]\Coubs 
  * Actual coubs downloaded by the downloader

## Contributors

Downloader login in python was written by [artemtar](https://github.com/artemtar/CoubDownloader).
I forked his repo and made several adjustments to avoid common formatting issues
and other minor problems.

## Known problems
* Some titles are so much crazy that they will appear in root directory with
just plain coub ID