from PIL import Image
import os

def convert_to_ico(source, target):
    try:
        img = Image.open(source)
        # Resize to standard icon sizes if needed, or just let Pillow handle it
        # Windows icons typically include 16, 32, 48, 64, 128, 256 sizes
        img.save(target, format='ICO', sizes=[(256, 256)])
        print(f"Successfully converted {source} to {target}")
    except Exception as e:
        print(f"Error converting icon: {e}")
        exit(1)

if __name__ == "__main__":
    convert_to_ico("d:/kaifa/apphier/AppHider/icon.png", "d:/kaifa/apphier/AppHider/app.ico")
