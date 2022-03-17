#!/usr/bin/env python3

import sys
import os
import time
import json
import subprocess
from fnmatch import fnmatch

import urllib.error
from urllib.request import urlopen, urlretrieve
from urllib.parse import quote as urlquote
import os, ssl

if (not os.environ.get('PYTHONHTTPSVERIFY', '') and getattr(ssl, '_create_unverified_context', None)):
    ssl._create_default_https_context = ssl._create_unverified_context

# TODO
# -) implement --limit-rate
# -) look out for new API changes

# Error codes
# 1 -> missing required software
# 2 -> invalid user-specified option
# 3 -> misc. runtime error (missing function argument, unknown value in case, etc.)
# 4 -> not all input coubs exist after execution (i.e. some downloads failed)
# 5 -> termination was requested mid-way by the user (i.e. Ctrl+C)
err_stat = {'dep': 1,
            'opt': 2,
            'run': 3,
            'down': 4,
            'int': 5}

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~
# Classes
# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

class Options:
    """Stores general options"""

    # Change verbosity of the script
    # 0 for quiet, >= 1 for normal verbosity
    verbosity = 1

    # Allowed values: yes, no, prompt
    prompt_answer = "prompt"

    # Default download destination
    path = "."

    # Keep individual video/audio streams
    keep = False

    # How often to loop the video
    # If longer than audio duration -> audio decides length
    repeat = 1000

    # Max. coub duration (FFmpeg syntax)
    dur = None

    # Pause between downloads (in sec)
    sleep_dur = None

    # Limit how many coubs can be downloaded during one script invocation
    max_coubs = None

    # Default sort order
    sort = "newest"

    # What video/audio quality to download
    #  0 -> worst quality
    # -1 -> best quality
    # Everything else can lead to undefined behavior
    v_quality = -1
    a_quality = -1

    # How much to prefer AAC audio
    # 0 -> never download AAC audio
    # 1 -> rank it between low and high quality MP3
    # 2 -> prefer AAC, use MP3 fallback
    # 3 -> either AAC or no audio
    aac = 1

    # Use shared video+audio instead of merging separate streams
    share = False

    # Download reposts during channel downloads
    recoubs = True

    # ONLY download reposts during channel downloads
    only_recoubs = False

    # Show preview after each download with the given command
    preview = False
    preview_command = "mpv"

    # Only download video/audio stream
    # Can't be both true!
    a_only = False
    v_only = False

    # Output parsed coubs to file instead of downloading
    # DO NOT TOUCH!
    out_file = None

    # Use an archive file to keep track of downloaded coubs
    archive_file = None

    # Output name formatting (default: %id%)
    # Supports the following special keywords:
    #   %id%        - coub ID (identifier in the URL)
    #   %title%     - coub title
    #   %creation%  - creation date/time
    #   %category%  - coub category
    #   %channel%   - channel title
    #   %tags%      - all tags (separated by tag_sep, see below)
    # All other strings are interpreted literally.
    #
    # Setting a custom value severely increases skip duration for existing coubs
    # Usage of an archive file is recommended in such an instance
    out_format = None

    # Advanced settings
    page_limit = 99           # used for tags; must be <= 99
    coubs_per_page = 25       # allowed: 1-25
    concat_list = "list.txt"
    tag_sep = "_"

class CoubInputData:
    """Stores coub-related data (e.g. links)"""

    links = []
    lists = []
    channels = []
    tags = []
    searches = []
    parsed = []

    def parse_links(self):
        """Parse direct input links from the command line"""

        for link in self.links:
            if opts.max_coubs and len(self.parsed) >= opts.max_coubs:
                break
            self.parsed.append(link)

        if self.links:
            msg("Reading command line:")
            msg("  ", len(self.links), " link(s) found", sep="")

    # ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

    def parse_lists(self):
        """Parse coub links from input lists"""

        for l in self.lists:
            msg("Reading input list (", l, "):", sep="")

            with open(l, "r") as f:
                content = f.read()

            # Replace tabs and spaces with newlines
            # Emulates default wordsplitting in Bash
            content = content.replace("\t", "\n")
            content = content.replace(" ", "\n")
            content = content.splitlines()

            for link in content:
                if opts.max_coubs and len(self.parsed) >= opts.max_coubs:
                    break
                self.parsed.append(link)

            msg("  ", len(content), " link(s) found", sep="")

    # ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

    def parse_timeline(self, url_type, url):
        """
        Parse coub links from various Coub source

        Currently supports
        -) channels
        -) tags
        -) coub searches
        """

        if url_type == "channel":
            channel = url.split("/")[-1]
            req = "https://coub.com/api/v2/timeline/channel/" + channel
            req += "?"
        elif url_type == "tag":
            tag = url.split("/")[-1]
            tag = urlquote(tag)
            req = "https://coub.com/api/v2/timeline/tag/" + tag
            req += "?"
        elif url_type == "search":
            search = url.split("=")[-1]
            search = urlquote(search)
            req = "https://coub.com/api/v2/search/coubs?q=" + search
            req += "&"
        else:
            err("Error: Unknown input type in parse_timeline!")
            clean()
            sys.exit(err_stat['run'])

        req += "per_page=" + str(opts.coubs_per_page)

        if opts.sort == "oldest" and url_type in ("tag", "search"):
            req += "&order_by=oldest"
        # Don't do anything for newest (as it's the default)
        # check_options already got rid of invalid values
        elif opts.sort != "newest":
            req += "&order_by=" + opts.sort

        req_json = urlopen(req).read()
        req_json = json.loads(req_json)

        pages = req_json['total_pages']

        msg("Downloading ", url_type, " info (", url, "):", sep="")

        for p in range(1, pages+1):
            # tag timeline redirects pages >99 to page 1
            # channel timelines work like intended
            if url_type == "tag" and p > opts.page_limit:
                msg("  Max. page limit reached!")
                return

            msg("  ", p, " out of ", pages, " pages", sep="")
            req_json = urlopen(req + "&page=" + str(p)).read()
            req_json = json.loads(req_json)

            for c in range(opts.coubs_per_page):
                if opts.max_coubs and len(self.parsed) >= opts.max_coubs:
                    return

                try:
                    c_id = req_json['coubs'][c]['recoub_to']['permalink']
                    if not opts.recoubs:
                        continue
                    self.parsed.append("https://coub.com/view/" + c_id)
                except (TypeError, KeyError, IndexError):
                    if opts.only_recoubs:
                        continue
                    try:
                        c_id = req_json['coubs'][c]['permalink']
                        self.parsed.append("https://coub.com/view/" + c_id)
                    except (TypeError, KeyError, IndexError):
                        continue


    # ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

    def parse_input(self):
        """Parse coub links from all available sources"""

        self.parse_links()
        self.parse_lists()
        for c in self.channels:
            self.parse_timeline("channel", c)
        for t in self.tags:
            self.parse_timeline("tag", t)
        for s in self.searches:
            self.parse_timeline("search", s)

        if not self.parsed:
            err("Error: No coub links specified!")
            clean()
            sys.exit(err_stat['opt'])

        if opts.max_coubs and len(self.parsed) >= opts.max_coubs:
            msg("\nDownload limit (", opts.max_coubs, ") reached!", sep="")

        if opts.out_file:
            with open(opts.out_file, "w") as f:
                for link in self.parsed:
                    print(link, file=f)
            msg("\nParsed coubs written to '", opts.out_file, "'!", sep="")
            clean()
            sys.exit(0)

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~
# Functions
# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def err(*args, **kwargs):
    """Print to stderr"""
    print(*args, file=sys.stderr, **kwargs)

def msg(*args, **kwargs):
    """Print to stdout based on verbosity level"""
    if opts.verbosity >= 1:
        print(*args, **kwargs)

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def usage():
    """Print help text"""

    print(f"""CoubDownloader is a simple download script for coub.com

Usage: {os.path.basename(sys.argv[0])} [OPTIONS] INPUT [INPUT]...

Input:
  LINK                   download specified coubs
  -l, --list LIST        read coub links from a text file
  -c, --channel CHANNEL  download all coubs from a channel
  -t, --tag TAG          download all coubs with the specified tag
  -e, --search TERM      download all search results for the given term

Common options:
  -h, --help             show this help
  -q, --quiet            suppress all non-error/prompt messages
  -y, --yes              answer all prompts with yes
  -n, --no               answer all prompts with no
  -s, --short            disable video looping
  -p, --path PATH        set output destination (default: '{opts.path}')
  -k, --keep             keep the individual video/audio parts
  -r, --repeat N         repeat video N times (default: until audio ends)
  -d, --duration TIME    specify max. coub duration (FFmpeg syntax)

Download options:
  --sleep TIME           pause the script for TIME seconds before each download
  --limit-num LIMIT      limit max. number of downloaded coubs
  --sort ORDER           specify download order for channels/tags
                         Allowed values:
                           newest (default)      likes_count
                           newest_popular        views_count
                           oldest (tags/search only)

Format selection:
  --bestvideo            Download best available video quality (default)
  --worstvideo           Download worst available video quality
  --bestaudio            Download best available audio quality (default)
  --worstaudio           Download worst available audio quality
  --aac                  Prefer AAC over higher quality MP3 audio
  --aac-strict           Only download AAC audio (never MP3)
  --share                Download 'share' video (shorter and includes audio)

Channel options:
  --recoubs              include recoubs during channel downloads (default)
  --no-recoubs           exclude recoubs during channel downloads
  --only-recoubs         only download recoubs during channel downloads

Preview options:
  --preview COMMAND      play finished coub via the given command
  --no-preview           explicitly disable coub preview

Misc. options:
  --audio-only           only download audio streams
  --video-only           only download video streams
  --write-list FILE      write all parsed coub links to FILE
  --use-archive FILE     use FILE to keep track of already downloaded coubs

Output:
  -o, --output FORMAT    save output with the specified name (default: %id%)

    Special strings:
      %id%        - coub ID (identifier in the URL)
      %title%     - coub title
      %creation%  - creation date/time
      %category%  - coub category
      %channel%   - channel title
      %tags%      - all tags (separated by '{opts.tag_sep}')

    Other strings will be interpreted literally.
    This option has no influence on the file extension.""")

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def check_prereq():
    """check existence of required software"""

    try:
        subprocess.run(["ffmpeg"], stdout=subprocess.DEVNULL, \
                                   stderr=subprocess.DEVNULL)
    except FileNotFoundError:
        err("Error: FFmpeg not found!")
        sys.exit(err_stat['dep'])

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def parse_cli():
    """Parse command line"""
    global opts, coubs

    if not sys.argv[1:]:
        usage()
        sys.exit(0)

    with_arg = ["-l", "--list",
                "-c", "--channel",
                "-t", "--tag",
                "-e", "--search",
                "-p", "--path",
                "-r", "--repeat",
                "-d", "--duration",
                "--sleep",
                "--limit-num",
                "--sort",
                "--preview",
                "--write-list",
                "--use-archive",
                "-o", "--output"]

    pos = 1
    while pos < len(sys.argv):
        opt = sys.argv[pos]
        if opt in with_arg:
            try:
                arg = sys.argv[pos+1]
            except IndexError:
                err("Missing value for ", opt, "!", sep="")
                sys.exit(err_stat['opt'])

            pos += 2
        else:
            pos += 1

        try:
            # Input
            if fnmatch(opt, "*coub.com/view/*"):
                coubs.links.append(opt.strip("/"))
            elif opt in ("-l", "--list"):
                if os.path.exists(arg):
                    coubs.lists.append(os.path.abspath(arg))
                else:
                    err("'", arg, "' is no valid list.", sep="")
            elif opt in ("-c", "--channel"):
                coubs.channels.append(arg.strip("/"))
            elif opt in ("-t", "--tag"):
                coubs.tags.append(arg.strip("/"))
            elif opt in ("-e", "--search"):
                coubs.searches.append(arg.strip("/"))
            # Common options
            elif opt in ("-h", "--help"):
                usage()
                sys.exit(0)
            elif opt in ("-q", "--quiet"):
                opts.verbosity = 0
            elif opt in ("-y", "--yes"):
                opts.prompt_answer = "yes"
            elif opt in ("-n", "--no"):
                opts.prompt_answer = "no"
            elif opt in ("-s", "--short"):
                opts.repeat = 1
            elif opt in ("-p", "--path"):
                opts.path = arg
            elif opt in ("-k", "--keep"):
                opts.keep = True
            elif opt in ("-r", "--repeat"):
                opts.repeat = int(arg)
            elif opt in ("-d", "--duration"):
                opts.dur = arg
            # Download options
            elif opt in ("--sleep",):
                opts.sleep_dur = float(arg)
            elif opt in ("--limit-num",):
                opts.max_coubs = int(arg)
            elif opt in ("--sort",):
                opts.sort = arg
            # Format selection
            elif opt in ("--bestvideo",):
                opts.v_quality = -1
            elif opt in ("--worstvideo",):
                opts.v_quality = 0
            elif opt in ("--bestaudio",):
                opts.a_quality = -1
            elif opt in ("--worstaudio",):
                opts.a_quality = 0
            elif opt in ("--aac",):
                opts.aac = 2
            elif opt in ("--aac-strict",):
                opts.aac = 3
            elif opt in ("--share",):
                opts.share = True
            # Channel options
            elif opt in ("--recoubs",):
                opts.recoubs = True
            elif opt in ("--no-recoubs",):
                opts.recoubs = False
            elif opt in ("--only-recoubs",):
                opts.only_recoubs = True
            # Preview options
            elif opt in ("--preview",):
                opts.preview = True
                opts.preview_command = arg
            elif opt in ("--no-preview",):
                opts.preview = False
            # Misc options
            elif opt in ("--audio-only",):
                opts.a_only = True
            elif opt in ("--video-only",):
                opts.v_only = True
            elif opt in ("--write-list",):
                opts.out_file = os.path.abspath(arg)
            elif opt in ("--use-archive",):
                opts.archive_file = os.path.abspath(arg)
            # Output
            elif opt in ("-o", "--output"):
                opts.out_format = arg
            # Unknown options
            elif fnmatch(opt, "-*"):
                err("Unknown flag '", opt, "'!", sep="")
                err("Try '", os.path.basename(sys.argv[0]), \
                    " --help' for more information.", sep="")
                sys.exit(err_stat['opt'])
            else:
                err("'", opt, "' is neither an opt nor a coub link!", sep="")
                err("Try '", os.path.basename(sys.argv[0]), \
                    " --help' for more information.", sep="")
                sys.exit(err_stat['opt'])
        except ValueError:
            err("Invalid ", opt, " ('", arg, "')!", sep="")
            sys.exit(err_stat['opt'])

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def check_options():
    """Check validity of command line options"""

    if opts.repeat <= 0:
        err("-r/--repeat must be greater than 0!")
        sys.exit(err_stat['opt'])
    elif opts.max_coubs and opts.max_coubs <= 0:
        err("--limit-num must be greater than zero!")
        sys.exit(err_stat['opt'])

    if opts.dur:
        command = ["ffmpeg", "-v", "quiet",
                   "-f", "lavfi", "-i", "anullsrc",
                   "-t", opts.dur, "-c", "copy",
                   "-f", "null", "-"]
        try:
            subprocess.check_call(command)
        except subprocess.CalledProcessError:
            err("Invalid duration! For the supported syntax see:")
            err("https://ffmpeg.org/ffmpeg-utils.html#time-duration-syntax")
            sys.exit(err_stat['opt'])

    if opts.a_only and opts.v_only:
        err("--audio-only and --video-only are mutually exclusive!")
        sys.exit(err_stat['opt'])
    elif not opts.recoubs and opts.only_recoubs:
        err("--no-recoubs and --only-recoubs are mutually exclusive!")
        sys.exit(err_stat['opt'])
    elif opts.share and (opts.v_only or opts.a_only):
        err("--share and --video-/audio-only are mutually exclusive!")
        sys.exit(err_stat['opt'])

    allowed_sort = ["newest",
                    "oldest",
                    "newest_popular",
                    "likes_count",
                    "views_count"]
    if opts.sort not in allowed_sort:
        err("Invalid sort order ('", opts.sort, "')!", sep="")
        sys.exit(err_stat['opt'])

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def resolve_paths():
    """Handle output path"""

    if not os.path.exists(opts.path):
        os.mkdir(opts.path)
    os.chdir(opts.path)

    if os.path.exists(opts.concat_list):
        err("Error: Reserved filename ('", opts.concat_list, "') " \
            "exists in '", opts.path, "'!", sep="")
        sys.exit(err_stat['run'])

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def get_name(req_json, c_id):
    """Decide filename for output file"""

    if not opts.out_format:
        return c_id

    name = opts.out_format
    name = name.replace("%id%", c_id)
    name = name.replace("%creation%", req_json['created_at'])
    name = name.replace("%channel%", req_json['channel']['title'])
    # Coubs don't necessarily have a category
    try:
        name = name.replace("%category%", req_json['categories'][0]['permalink'])
    except (KeyError, TypeError, IndexError):
        name = name.replace("%category%", "")

    tags = ""
    for t in req_json['tags']:
        tags += t['title'] + opts.tag_sep
    name = name.replace("%tags%", tags)

    # Strip/replace special characters that can lead to script failure (ffmpeg concat)
    # ' common among coub titles
    # Newlines can be occasionally found as well
    title = req_json['title']
    title = title.replace("'", "")
    title = title.replace("\n", " ")
    title = title.replace("\r", " ")
    title = title.replace("/","")
    title = title.replace("?","")
    title = title.replace("|","")
    title = title.replace(")","")
    title = title.replace("*","")
    title = title.replace("(","")
    title = title.replace(":","")
    title = title.replace("\\","")
	
    # Insert actual sanitized title
    name = name.replace("%title%", title)
	
    # First try the original filename
    try:
        f = open(name, "w")
        f.close()
        os.remove(name)
    except Exception:
        err("Warning: Filename has some unsupported characters, trying to fix that. ", end="")
        name = ''.join([i if ord(i) < 128 else '_' for i in name]).replace("?","")
        err("Trying '", name, "'.", sep="")
        # Try second time with sanitized filenames
        try:
            f = open(name, "w")
            f.close()
            os.remove(name)
        except OSError:
            # Fallback to ID
            err("Error: Filename invalid or too long! ", end="")
            err("Falling back to '", c_id, "'.", sep="")
            name = '\\'.join(opts.out_format.split('\\')[0:-1]) + "\\" + c_id

    return name

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def exists(name):
    """Check if a coub with given name already exists"""

    if opts.v_only:
        full_name = [name + ".mp4"]
    elif opts.a_only:
        # exists() gets possibly called before the API request
        # to be safe check for both possible audio extensions
        full_name = [name + ".mp3", name + ".m4a"]
    else:
        full_name = [name + ".mp4"]

    for f in full_name:
        if os.path.exists(f):
            return True

    return False

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def overwrite():
    """Decide if existing coub should be overwritten"""
    return False # Never overwrite
    if opts.prompt_answer == "yes":
        return True
    elif opts.prompt_answer == "no":
        return False
    elif opts.prompt_answer == "prompt":
        print("Overwrite file?")
        print("1) yes")
        print("2) no")
        while True:
            answer = input("#? ")
            if answer == "1":
                return True
            if answer == "2":
                return False
    else:
        err("Unknown prompt_answer in overwrite!")
        clean()
        sys.exit(err_stat['run'])

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def read_archive(c_id):
    """Check archive file for coub ID"""

    if not os.path.exists(opts.archive_file):
        return False

    with open(opts.archive_file, "r") as f:
        content = f.readlines()
    for l in content:
        if l == c_id + "\n":
            return True

    return False

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def write_archive(c_id):
    """Output coub ID to archive file"""

    with open(opts.archive_file, "a") as f:
        print(c_id, file=f)

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def stream_lists(data):
    """Collect available video/audio streams of a coub"""

    # A few words (or maybe more) regarding Coub's streams:
    #
    # 'html5' has 3 video and 2 audio qualities
    #     video: med    (~360p)
    #            high   (~720p)
    #            higher (~900p)
    #     audio: med    (MP3@128Kbps CBR)
    #            high   (MP3@160Kbps VBR)
    #
    # 'mobile' has 1 video and 2 audio qualities
    #     video: video  (~360p)
    #     audio: 0      (AAC@128Kbps CBR or MP3@128Kbps CBR)
    #            1      (MP3@128Kbps CBR)
    #
    # 'share' has 1 quality (audio+video)
    #     video+audio: default (~720p, sometimes ~360p + AAC@128Kbps CBR)
    #
    # -) all videos come with a watermark
    # -) html5 video/audio and mobile audio may come in less available qualities
    # -) html5 video med and mobile video are the same file
    # -) html5 audio med and the worst mobile audio are the same file
    # -) mobile audio 0 is always the best mobile audio
    # -) often only mobile audio 0 is available as MP3 (no mobile audio 1)
    # -) share video has the same quality as mobile video
    # -) share audio is always AAC, even if mobile audio is only available as MP3
    # -) share audio is often shorter than other audio versions
    # -) videos come as MP4, MP3 audio as MP3 and AAC audio as M4A.
    #
    # All the aforementioned information regards the new Coub storage system (after the watermark introduction).
    # Also Coub is still catching up with encoding, so not every stream existence is yet guaranteed.
    #
    # Streams that may still be unavailable:
    #   -) share
    #   -) mobile video with direct URL (not the old base64 format)
    #   -) mobile audio in AAC
    #   -) html5 video higher
    #   -) html5 video med/high in a non-broken state (don't require \x00\x00 fix)
    #
    # There are no universal rules in which order new streams get added.
    # Sometimes you find videos with non-broken html5 streams, but the old base64 mobile URL.
    # Sometimes you find videos without html5 higher, but with the new mobile video.
    # Sometimes only html5 video med is still broken.
    #
    # It's a mess. Also release an up-to-date API documentations, you dolts!

    video = []
    audio = []

    if opts.share:
        try:
            version = data['file_versions']['share']['default']
            # Non-existence should result in None
            # Unfortunately there are exceptions to this rule (e.g. '{}')
            if not version or version in ("{}",):
                raise KeyError
        except KeyError:
            return ([], [])
        return ([version], [])

    for vq in ["med", "high", "higher"]:
        try:
            version = data['file_versions']['html5']['video'][vq]
        except KeyError:
            continue

        # v_size/a_size can be 0 OR None in case of a missing stream
        # None is the exception and an irregularity in the Coub API
        if version['size']:
            video.append(version['url'])

    if opts.aac >= 2:
        a_combo = [("html5", "med"), ("html5", "high"), ("mobile", 0)]
    else:
        a_combo = [("html5", "med"), ("mobile", 0), ("html5", "high")]

    for form, aq in a_combo:
        try:
            version = data['file_versions'][form]['audio'][aq]
        except KeyError:
            continue

        if form == "mobile":
            # Mobile audio doesn't list size
            # So just pray that the file behind the link exists
            if opts.aac:
                audio.append(version)
        else:
            if version['size'] and opts.aac < 3:
                audio.append(version['url'])

    return (video, audio)

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def download(v_link, a_link, a_ext, name):
    """Download individual coub streams"""

    if not opts.a_only:
        try:
            urlretrieve(v_link, name + ".mp4")
        except (IndexError, urllib.error.HTTPError):
            err("Error: Coub unavailable!")
            raise

    if not opts.v_only and a_link:
        try:
            urlretrieve(a_link, name + "." + a_ext)
        except (IndexError, urllib.error.HTTPError):
            if opts.a_only:
                err("Error: Audio or coub unavailable!")
                raise

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def merge(a_ext, name):
    """Merge video/audio stream with ffmpeg and loop video"""

    err("file '" + name + ".mp4'", sep="")
    # Print .txt for ffmpeg's concat
    with open(opts.concat_list, "w") as f:
        for i in range(opts.repeat):
            print("file '" + name + ".mp4'", file=f)

    # Loop footage until shortest stream ends
    # Concatenated video (via list) counts as one long stream
    command = ["ffmpeg", "-y", "-v", "error",
               "-f", "concat", "-safe", "0",
               "-i", opts.concat_list, "-i", name + "." + a_ext]

    if opts.dur:
        command.extend(["-t", opts.dur])

    output_ext = ".mp4"
    tmp_name = name + "_" + output_ext
    command.extend(["-c", "copy", "-shortest", tmp_name])

    subprocess.run(command)

    if not opts.keep:
        os.remove(name + ".mp4")
        os.remove(name + "." + a_ext)
        
    os.rename(tmp_name, name + output_ext)

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def show_preview(a_ext, name):
    """Play finished coub with the given command"""

    # For normal downloads .mkv, unless error downloading audio
    if os.path.exists(name + ".mkv"):
        ext = ".mkv"
    else:
        ext = ".mp4"
    if opts.a_only:
        ext = "." + a_ext
    if opts.v_only:
        ext = ".mp4"

    try:
        # Need to split command string into list for check_call
        command = opts.preview_command.split(" ")
        command.append(name + ext)
        subprocess.check_call(command, stdout=subprocess.DEVNULL, \
                                       stderr=subprocess.DEVNULL)
    except subprocess.CalledProcessError:
        err("Error: Missing file, invalid command or user interrupt in show_preview!")
        raise

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def clean():
    """Clean workspace"""

    if os.path.exists(opts.concat_list):
        os.remove(opts.concat_list)

# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~
# Main Function
# ~~~~~~~~~~~~~~~~~~~~~~~~~~~~

def main():
    """Main function body"""

    check_prereq()
    parse_cli()
    check_options()
    resolve_paths()

    msg("\n### Parse Input ###\n")
    coubs.parse_input()

    msg("\n### Download Coubs ###\n")
    count = 0
    done = 0
    for c in coubs.parsed:
        count += 1
        msg("  ", count, " out of ", len(coubs.parsed), " (", c, ")", sep="")

        c_id = c.split("/")[-1]

        # Pass existing files to avoid unnecessary downloads
        # This check handles archive file search and default output formatting
        # Avoids json request (slow!) just to skip files anyway
        if (opts.archive_file and read_archive(c_id)) or \
           (not opts.out_format and exists(c_id) and not overwrite()):
            msg("Already downloaded!")
            clean()
            continue

        req = "https://coub.com/api/v2/coubs/" + c_id
        try:
            req_json = urlopen(req).read()
        except urllib.error.HTTPError:
            err("Error: Coub unavailable!")
            continue
        req_json = json.loads(req_json)

        name = get_name(req_json, c_id)

        # Get link list and assign final download URL
        v_list, a_list = stream_lists(req_json)
        try:
            v_link = v_list[opts.v_quality]
        except IndexError:
            err("Error: Coub unavailable!")
            continue
        try:
            a_link = a_list[opts.a_quality]
            # Audio can be MP3 (.mp3) or AAC (.m4a)
            a_ext = a_link.split(".")[-1]
        except IndexError:
            if opts.a_only:
                err("Error: Audio or coub unavailable!")
                continue
            a_link = None
            a_ext = None

        # Another check for custom output formatting
        # Far slower to skip existing files (archive usage is recommended)
        if opts.out_format and exists(name) and not overwrite():
            msg("Already downloaded!")
            clean()
            done += 1
            continue

        if opts.sleep_dur and count > 1:
            time.sleep(opts.sleep_dur)

        # Download video/audio streams
        # Skip if the requested media couldn't be downloaded
        try:
            download(v_link, a_link, a_ext, name)
        except (IndexError, urllib.error.HTTPError):
            continue

        # Merge video and audio
        if not opts.v_only and not opts.a_only and a_link:
            merge(a_ext, name)

        # Write downloaded coub to archive
        if opts.archive_file:
            write_archive(c_id)

        # Preview downloaded coub
        if opts.preview:
            try:
                show_preview(a_ext, name)
            except subprocess.CalledProcessError:
                pass

        # Clean workspace
        clean()

        # Record successful download
        done += 1

    msg("\n### Finished ###\n")

    # Indicate failure if not all input coubs exist after execution
    if done < count:
        sys.exit(err_stat['down'])

# Execute main function
if __name__ == '__main__':
    opts = Options()
    coubs = CoubInputData()

    try:
        main()
    except KeyboardInterrupt:
        err("User Interrupt!")
        clean()
        sys.exit(err_stat['int'])
    sys.exit(0)
