from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.backends import default_backend
import base64

def generate(key_size: int, privname: str, publicname: str, capi=True) -> None:
    """
    generate private and public key and save to a file
    """
    private_key = rsa.generate_private_key(
        public_exponent=65537,
        key_size=key_size,
        backend=default_backend()
    )

    with open(privname, "wb") as f:
        f.write(
            private_key.private_bytes(
                encoding=serialization.Encoding.PEM,
                format=serialization.PrivateFormat.TraditionalOpenSSL,
                encryption_algorithm=serialization.NoEncryption()
            )
        )

    public_key = private_key.public_key()
    
    if capi:
        blob = getCAPIpubblob(public_key)
        blobB64 = base64.b64encode(blob)
        with open(publicname, "wb") as f:
            f.write(blobB64)
    else:
        with open(publicname, "wb") as f:
            f.write(
            public_key.public_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PublicFormat.SubjectPublicKeyInfo
            )
        )

def getCAPIpubblob(pubkey) -> bytes:
    """
    convert publickey to capi PUBLICKEYBLOB format (for 2016 client and RCC)
    """
    numbers = pubkey.public_numbers()
    modulus = numbers.n
    exponent = numbers.e

    modulusB = modulus.to_bytes((modulus.bit_length() + 7) // 8, byteorder='little')
    exp = exponent.to_bytes(4, byteorder='little')

    bitlen = modulus.bit_length()

    blob = bytearray()

    blob.extend([
        0x06,
        0x02,
        0x00, 0x00
    ])
    blob.extend((0x00002400).to_bytes(4, 'little'))

    blob.extend(b'RSA1')
    blob.extend(bitlen.to_bytes(4, 'little'))
    blob.extend(exp)

    blob.extend(modulusB)

    return bytes(blob)

generate(1024, "PrivateKey.pem", "PublicKey2016.pub", capi=True)
generate(2048, "PrivateKey2020.pem", "PublicKey2020.pub", capi=False)
