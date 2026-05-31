import struct, zlib

width, height = 64, 64
pixels = []
for y in range(height):
    row = b'\x00'
    for x in range(width):
        cx, cy = width//2, height//2
        dx, dy = abs(x - cx), abs(y - cy)
        if dx*dx + dy*dy < 800 and y > 10 and y < 54:
            if (dx > 4 and dy > 4) or (dx < 14 and dy < 18 and y > 20 and y < 32):
                r, g, b, a = 100, 120, 100, 255
            else:
                r, g, b, a = 40, 40, 40, 255
        else:
            r, g, b, a = 0, 0, 0, 0
        row += struct.pack('BBBB', r, g, b, a)
    pixels.append(row)

def make_png(w, h, rows):
    PNG_HEADER = b'\x89PNG\r\n\x1a\n'
    ihdr_data = struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)
    def chunk(ctype, data):
        c = ctype + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    raw = b''.join(rows)
    compressed = zlib.compress(raw)
    return PNG_HEADER + chunk(b'IHDR', ihdr_data) + chunk(b'IDAT', compressed) + chunk(b'IEND', b'')

png = make_png(width, height, pixels)
path = r'C:\Users\dasha\Projects\vinc-engine\icon.png'
with open(path, 'wb') as f:
    f.write(png)
print(f'Wrote {len(png)} bytes to icon.png')
