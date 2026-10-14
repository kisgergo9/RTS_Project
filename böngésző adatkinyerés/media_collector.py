import asyncio
import csv
import mimetypes
import re
import sys
import uuid

from datetime import datetime
from pathlib import Path
from urllib.parse import urlparse, unquote

from playwright.async_api import async_playwright


# ============================================================
# BEÁLLÍTÁSOK
# ============================================================

BASE_DIR = Path(__file__).resolve().parent / "media_letoltes"
PROFILE_DIR = BASE_DIR / "chrome_profile"

IMAGE_DIR = BASE_DIR / "kepek"
AUDIO_DIR = BASE_DIR / "hangok"
VIDEO_DIR = BASE_DIR / "videok"
OTHER_DIR = BASE_DIR / "egyeb_media"
LOG_FILE = BASE_DIR / "network_log.csv"

# Ennyinél nagyobb fájlt a program kihagy.
# Állítsd None-ra, ha nem szeretnél méretkorlátot.
# A válasz ilyenkor is teljes egészében memóriába kerülhet!
MAX_FILE_SIZE_MB = 200

# Csak ezeknek a MIME-típusoknak a mentése.
MEDIA_PREFIXES = ("image/", "audio/", "video/")

MEDIA_MIME_TYPES = {
    "application/ogg",
    "application/mp4",
    "application/vnd.apple.mpegurl",
    "application/x-mpegurl",
    "application/dash+xml",
    "application/vnd.ms-sstr+xml",
}

# Kiterjesztések, ha a mimetypes modul nem ad megfelelőt.
MIME_EXTENSIONS = {
    "image/jpeg": ".jpg",
    "image/png": ".png",
    "image/gif": ".gif",
    "image/webp": ".webp",
    "image/svg+xml": ".svg",
    "image/avif": ".avif",
    "audio/mpeg": ".mp3",
    "audio/mp4": ".m4a",
    "audio/aac": ".aac",
    "audio/ogg": ".ogg",
    "audio/wav": ".wav",
    "audio/webm": ".webm",
    "video/mp4": ".mp4",
    "video/webm": ".webm",
    "video/ogg": ".ogv",
    "video/quicktime": ".mov",
    "video/x-msvideo": ".avi",
    "video/mp2t": ".ts",
    "application/ogg": ".ogg",
    "application/mp4": ".mp4",
    "application/vnd.apple.mpegurl": ".m3u8",
    "application/x-mpegurl": ".m3u8",
    "application/dash+xml": ".mpd",
}


# ============================================================
# MAPPA ÉS NAPLÓ
# ============================================================

for directory in (
    BASE_DIR,
    IMAGE_DIR,
    AUDIO_DIR,
    VIDEO_DIR,
    OTHER_DIR,
    PROFILE_DIR,
):
    directory.mkdir(parents=True, exist_ok=True)

LOG_FIELDS = [
    "ido",
    "url",
    "status",
    "mime_tipus",
    "meret_byte",
    "fajl",
    "eredmeny",
    "hiba",
]

if not LOG_FILE.exists():
    with LOG_FILE.open("w", newline="", encoding="utf-8-sig") as f:
        writer = csv.DictWriter(f, fieldnames=LOG_FIELDS, delimiter=";")
        writer.writeheader()


def log_event(
    url,
    status="",
    mime="",
    size="",
    filepath="",
    result="",
    error="",
):
    """Egy esemény hozzáfűzése a CSV-naplóhoz."""
    try:
        with LOG_FILE.open("a", newline="", encoding="utf-8-sig") as f:
            writer = csv.DictWriter(
                f,
                fieldnames=LOG_FIELDS,
                delimiter=";",
            )
            writer.writerow({
                "ido": datetime.now().isoformat(timespec="seconds"),
                "url": url,
                "status": status,
                "mime_tipus": mime,
                "meret_byte": size,
                "fajl": filepath,
                "eredmeny": result,
                "hiba": error,
            })
    except OSError as exc:
        print(f"[NAPLÓHIBA] {exc}")


# ============================================================
# SEGÉDFÜGGVÉNYEK
# ============================================================

def get_extension(mime, url):
    """
    Először a MIME-típus, majd az URL alapján próbál
    megfelelő kiterjesztést választani.
    """
    mime = mime.lower().split(";")[0].strip()

    if mime in MIME_EXTENSIONS:
        return MIME_EXTENSIONS[mime]

    extension = mimetypes.guess_extension(mime)
    if extension:
        return extension

    url_extension = Path(unquote(urlparse(url).path)).suffix.lower()

    allowed_extensions = {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg",
        ".avif", ".bmp", ".ico",
        ".mp3", ".m4a", ".aac", ".ogg", ".wav", ".flac",
        ".opus", ".wma",
        ".mp4", ".webm", ".mov", ".avi", ".mkv", ".ogv",
        ".ts", ".m3u8", ".mpd",
    }

    if url_extension in allowed_extensions:
        return url_extension

    return ".bin"


def get_category(mime):
    mime = mime.lower().split(";")[0].strip()

    if mime.startswith("image/"):
        return IMAGE_DIR

    if mime.startswith("audio/"):
        return AUDIO_DIR

    if mime.startswith("video/"):
        return VIDEO_DIR

    return OTHER_DIR


def safe_filename(name):
    """Windows alatt is használható fájlnév készítése."""
    name = unquote(name)
    name = re.sub(r'[<>:"/\\|?*\x00-\x1f]', "_", name)
    name = name.strip(" .")

    if not name:
        name = "media"

    # Windows alatt a fájlnév ne legyen túl hosszú.
    return name[:120]


def make_unique_path(directory, filename):
    """Ütközésmentes fájlútvonal készítése."""
    path = directory / filename

    if not path.exists():
        return path

    stem = path.stem
    suffix = path.suffix

    while True:
        candidate = directory / f"{stem}_{uuid.uuid4().hex[:8]}{suffix}"
        if not candidate.exists():
            return candidate


def choose_filename(response, mime):
    """
    Content-Disposition fejlécből, URL-ből vagy véletlen
    azonosítóból állítja elő a fájlnevet.
    """
    headers = response.headers
    disposition = headers.get("content-disposition", "")

    filename = None

    # Content-Disposition: filename="..."
    match = re.search(
        r"""filename\*?=(?:UTF-8''|")?([^";]+)""",
        disposition,
        re.IGNORECASE,
    )

    if match:
        filename = match.group(1).strip().strip('"')

    if not filename:
        filename = Path(
            unquote(urlparse(response.url).path)
        ).name

    if not filename or filename in (".", ".."):
        filename = f"media_{uuid.uuid4().hex[:12]}"

    filename = safe_filename(filename)
    extension = get_extension(mime, response.url)

    # A MIME-típusból származó kiterjesztés az irányadó.
    current_suffix = Path(filename).suffix.lower()

    if not current_suffix:
        filename += extension
    elif current_suffix != extension:
        filename = str(Path(filename).with_suffix(extension))

    return filename


def is_media(mime):
    mime = mime.lower().split(";")[0].strip()
    return (
        mime.startswith(MEDIA_PREFIXES)
        or mime in MEDIA_MIME_TYPES
    )


# ============================================================
# HÁLÓZATI VÁLASZOK MENTÉSE
# ============================================================

async def save_response(response):
    url = response.url
    status = response.status
    headers = response.headers

    mime = headers.get("content-type", "")
    mime_clean = mime.lower().split(";")[0].strip()

    if not is_media(mime_clean):
        return

    # A sikertelen HTTP-válaszok tartalmát ne mentsük.
    if status < 200 or status >= 300:
        log_event(
            url,
            status=status,
            mime=mime,
            result="kihagyva",
            error="Nem sikeres HTTP-válasz",
        )
        return

    content_length = headers.get("content-length")

    if MAX_FILE_SIZE_MB is not None and content_length:
        try:
            expected_size = int(content_length)
            if expected_size > MAX_FILE_SIZE_MB * 1024 * 1024:
                log_event(
                    url,
                    status=status,
                    mime=mime,
                    size=expected_size,
                    result="kihagyva",
                    error=f"A fájl nagyobb a {MAX_FILE_SIZE_MB} MB-os korlátnál",
                )
                print(f"[MÉRET KORLÁT] {url}")
                return
        except ValueError:
            pass

    try:
        # A Playwright a teljes HTTP-választ beolvassa.
        body = await response.body()

        # Content-Length hiányában is ellenőrizzük a méretet.
        if (
            MAX_FILE_SIZE_MB is not None
            and len(body) > MAX_FILE_SIZE_MB * 1024 * 1024
        ):
            log_event(
                url,
                status=status,
                mime=mime,
                size=len(body),
                result="kihagyva",
                error=f"A fájl nagyobb a {MAX_FILE_SIZE_MB} MB-os korlátnál",
            )
            print(f"[MÉRET KORLÁT] {url}")
            return

        directory = get_category(mime_clean)
        filename = choose_filename(response, mime_clean)
        filepath = make_unique_path(directory, filename)

        filepath.write_bytes(body)

        log_event(
            url,
            status=status,
            mime=mime,
            size=len(body),
            filepath=str(filepath.relative_to(BASE_DIR)),
            result="mentve",
        )

        print(
            f"[MENTVE] {mime_clean} | "
            f"{len(body):,} byte | {filepath.name}"
        )

    except Exception as exc:
        log_event(
            url,
            status=status,
            mime=mime,
            result="hiba",
            error=f"{type(exc).__name__}: {exc}",
        )
        print(f"[HIBA] {url}\n       {exc}")


# ============================================================
# BÖNGÉSZŐ INDÍTÁSA
# ============================================================

async def main():
    tasks = set()

    async with async_playwright() as playwright:
        try:
            # Google Chrome telepített példányának indítása,
            # elkülönített felhasználói profillal.
            context = await playwright.chromium.launch_persistent_context(
                user_data_dir=str(PROFILE_DIR),
                channel="chrome",
                headless=False,
                accept_downloads=True,
            )

        except Exception as exc:
            print("\nNem sikerült elindítani a Google Chrome-ot.")
            print(f"Hiba: {exc}")
            print(
                "\nEllenőrizd, hogy a Chrome telepítve van-e, "
                "és a PROFILE_DIR mappát nem használja-e másik folyamat."
            )
            return

        def on_response(response):
            task = asyncio.create_task(save_response(response))
            tasks.add(task)
            task.add_done_callback(tasks.discard)

        context.on("response", on_response)

        # Legyen nyitott lap, amelyben el lehet kezdeni a böngészést.
        if not context.pages:
            await context.new_page()

        print("\n" + "=" * 60)
        print("MÉDIALETÖLTŐ ELINDULT")
        print("=" * 60)
        print(f"Mentési mappa: {BASE_DIR}")
        print(f"CSV-napló:     {LOG_FILE}")
        print("\nNavigálj a Chrome-ban a kívánt weboldalra.")
        print("A felismert médiafájlok automatikusan mentésre kerülnek.")
        print("Leállítás: zárd be a Chrome-ot, vagy nyomj Ctrl+C-t.")
        print("=" * 60)

        try:
            # A böngésző nyitva marad, amíg le nem állítod.
            while True:
                await asyncio.sleep(1)

        except (KeyboardInterrupt, asyncio.CancelledError):
            print("\nLeállítás folyamatban...")

        finally:
            # A már elindított mentések befejezésének megvárása.
            if tasks:
                await asyncio.gather(*list(tasks), return_exceptions=True)

            await context.close()
            print("A böngésző bezárult.")


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass