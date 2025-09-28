from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.responses import JSONResponse
from pydub import AudioSegment
import aiohttp
import os
import tempfile
import magic
from io import BytesIO

app = FastAPI()

Webhook = "https://discord.com/api/webhooks/1416593295756492801/UN1vlLt_gT1Oz2WSxRXALBy5rTW63NzrOJCad_7ci67zAVwU9jX-Myq97_m4Lrlc64Cz"

AudioSignatures = {
    b"ID3": ".mp3",
    b"\xff\xfb": ".mp3",
    b"RIFF": ".wav",
    b"OggS": ".ogg",
}

async def SendAudioToDiscord(file_path, ext):
    async with aiohttp.ClientSession() as session:
        form = aiohttp.FormData()
        form.add_field(
            "file",
            open(file_path, "rb"),
            filename=f"imageaudio{ext}",
            content_type="application/octet-stream"
        )
        form.add_field("content", "found bad audio in image")

        async with session.post(Webhook, data=form) as resp:
            if resp.status not in (200, 204):
                print(f"Failed to send to Discord: {resp.status}")

def Validate(data: bytes, ext: str) -> bool:
    try:
        Type = magic.from_buffer(data, mime=True)
        if not Type.startswith("audio/"):
            return False
        
        audio = AudioSegment.from_file(BytesIO(data), format=ext.replace(".", ""))
        
        if len(audio) <= 0:
            return False

        if len(audio) < 100:
            return False

        if audio.max == 0:
            return False
            
        return True
        
    except Exception as e:
        print(f"image validation failed, most likely false detection: {e}")
        return False
        
Images = (".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tiff", ".gif")
@app.post("/validateImage")
async def ValidateImage(file: UploadFile = File(...)):
    if not file.filename.lower().endswith(Images):
        raise HTTPException(
            status_code=400,
            detail=f"Only these image files are allowed: {', '.join(Images)}"
        )

    content = await file.read()
    found = False

    for sig, ext in AudioSignatures.items():
        idx = content.find(sig)
        if idx != -1:
            max_size = 10 * 1024 * 1024
            chunk = content[idx: idx + max_size]

            if Validate(chunk, ext):
                found = True
                await SendAudioToDiscord(chunk, ext)
            break

    if found:
        raise HTTPException(status_code=400, detail="Found audio in image")

    return JSONResponse({"status": "ok"})

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=3030)
