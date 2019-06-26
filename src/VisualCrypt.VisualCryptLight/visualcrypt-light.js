/*! 
 * VisualCrypt-light v0.1.0
 * https://github.com/visualcrypt
 * Diffie-Hellman key exchange and symmetric encryption between C# and JavaScript.
 * Algorithms used *): Curve25519, SHA-256, AES-256 CBC, deflate
 * License: MIT https://opensource.org/licenses/MIT
 * Copyright 2019 VisualCrypt AG
 * *) See algorithm copyright notices and licenses in the algorithm sections.
 */
"use strict";

const vcl = (function (window) {
    function createModel(clientPublicKey, cipherV2Bytes) {
        return {
            currentPublicKey: encodeBase64(clientPublicKey),
            cipherV2Bytes: encodeBase64(cipherV2Bytes)
        };
    }

    function generateKeyPair() {
        const randomSeed = rnd(32);
        return axlsign.generateKeyPair(randomSeed);
    }

    function encrypt(plainTextUint8Array, serverPublicKey, clientPrivateKey) {

        const hashedSharedSecret = calculateSharedSecret(serverPublicKey, clientPrivateKey);

        const version = 2;            // cipherV2.version
        const roundsExponent = 255;   // use sha256 hashing
        const iV = rnd(16);

        const compressed = pako.deflateRaw(plainTextUint8Array);
        const paddedData = applyRandomPadding(compressed);

        const inputDerivedKey = createDerivedKeyWithSha256(iV, hashedSharedSecret);
        const randomKey = rnd(32);

        const randomKeyCipher32 = new aesjs.ModeOfOperation.cbc(inputDerivedKey, iV).encrypt(randomKey);
        const messageCipher = new aesjs.ModeOfOperation.cbc(randomKey, iV).encrypt(paddedData.paddedData);

        const mac = createMacFromCipherV2(messageCipher, paddedData.padding, version);
        const macCipher16 = new aesjs.ModeOfOperation.cbc(randomKey, iV).encrypt(mac);

        const cipherV2Bytes = new Uint8Array(67 + messageCipher.length);
        cipherV2Bytes[0] = version;
        cipherV2Bytes[1] = roundsExponent;
        cipherV2Bytes[2] = paddedData.padding;
        cipherV2Bytes.set(iV, 3);
        cipherV2Bytes.set(macCipher16, 19);
        cipherV2Bytes.set(randomKeyCipher32, 35);
        cipherV2Bytes.set(messageCipher, 67);

        const model = createModel(window.clientKeyPair.public, cipherV2Bytes);
        return model;
    }

    function decrypt(cipherV2Bytes, serverPublicKey, clientPrivateKey) {

        const sharedSecret = calculateSharedSecret(serverPublicKey, clientPrivateKey);
        const cipherV2 = decodeCipherV2Bytes(cipherV2Bytes);

        const inputDerivedKey32 = createDerivedKeyWithSha256(cipherV2.iV16, sharedSecret);

        // The cipher-block chaining mode of operation maintains internal state, always create a new instance.
        const randomKey = new aesjs.ModeOfOperation.cbc(inputDerivedKey32, cipherV2.iV16).decrypt(cipherV2.randomKeyCipher32);

        const decryptedMac = new aesjs.ModeOfOperation.cbc(randomKey, cipherV2.iV16).decrypt(cipherV2.macCipher16);
        const actualMac = createMacFromCipherV2(cipherV2.messageCipher, cipherV2.plaintextPadding, cipherV2.version);
        for (let j = 0; j < decryptedMac.length; j++) {
            assert(decryptedMac[j] === actualMac[j], "Either the key or the data is not valid.");
        }

        const paddedData = new aesjs.ModeOfOperation.cbc(randomKey, cipherV2.iV16).decrypt(cipherV2.messageCipher);

        const compressed = removePadding(paddedData, cipherV2.plaintextPadding);
        const uncompressed = pako.inflateRaw(compressed);
        return uncompressed;
    }

    function decodeCipherV2Bytes(cipherV2Bytes) {
        const cipherV2 = {
            version: 2,
            roundsExponent: 255,
            plaintextPadding: 0,
            iV16: new Uint8Array(16),
            macCipher16: new Uint8Array(16),
            randomKeyCipher32: new Uint8Array(32),
            messageCipher: new Uint8Array(cipherV2Bytes.length - 67)
        };

        const [, roundsExponent, plaintextPadding] = cipherV2Bytes;

        if (cipherV2Bytes[0] !== cipherV2.version)
            throw "Expected a version byte at index 0 of value '2'.";

        if ((roundsExponent > 31 || roundsExponent < 4) && roundsExponent !== 0xff)
            throw "The value for the rounds exponent at index 1 is invalid.";

        if (plaintextPadding > 15)
            throw "The value at the padding byte at index 1 is invalid.";

        cipherV2.roundsExponent = roundsExponent;
        cipherV2.plaintextPadding = plaintextPadding;

        cipherV2.iV16.set(cipherV2Bytes.subarray(3, 3 + 16));
        cipherV2.macCipher16.set(cipherV2Bytes.subarray(19, 19 + 16));
        cipherV2.randomKeyCipher32.set(cipherV2Bytes.subarray(35, 35 + 32));
        cipherV2.messageCipher.set(cipherV2Bytes.subarray(67, 67 + cipherV2.messageCipher.length));
        return cipherV2;
    }

    function applyRandomPadding(compressed) {
        let requiredPadding = 0;
        if (compressed.length % 16 !== 0) {
            requiredPadding = 16 - compressed.length % 16;
        }
        if (requiredPadding === 0) {
            return {
                padding: requiredPadding,
                paddedData: compressed
            };
        }
        const paddedData = new Uint8Array(compressed.length + requiredPadding);
        paddedData.set(compressed);
        paddedData.set(rnd(requiredPadding), compressed.length);
        return {
            padding: requiredPadding,
            paddedData: paddedData
        };
    }

    function removePadding(paddedData, padding) {
        return paddedData.subarray(0, paddedData.length - padding);
    }

    function createMacFromCipherV2(messageCipher, paddingValue, version) {
        const includeInMac = new Uint8Array(messageCipher.length + 2);
        includeInMac.set(messageCipher);
        includeInMac[messageCipher.length] = paddingValue;
        includeInMac[messageCipher.length + 1] = version;
        const hash = new Uint8Array(sha256.array(includeInMac));
        const mac16 = hash.subarray(0, 16);
        assert(mac16.length === 16);
        return mac16;
    }

    function createDerivedKeyWithSha256(iv, key) {
        const keyMaterial64 = new Uint8Array(64);
        keyMaterial64.set(key); // leave upper 32 bytes zero
        const keyMaterial = new Uint8Array(80);
        keyMaterial.set(iv);
        keyMaterial.set(keyMaterial64, iv.length);

        const derivedKey = new Uint8Array(sha256.array(keyMaterial));
        return derivedKey;
    }

    function calculateSharedSecret(publicKeyUInt8, privateKeyUInt8) {
        const sharedKey = axlsign.sharedKey(privateKeyUInt8, publicKeyUInt8);
        const hashedSharedKey = new Uint8Array(sha256.array(sharedKey));
        return hashedSharedKey;
    }

    function rnd(len) {
        const buf = new Uint8Array(len);
        window.crypto.getRandomValues(buf);
        return buf;
    }

    function encodeBase64(uint8Array) {
        const base64String = btoa(String.fromCharCode.apply(null, uint8Array));
        return base64String;
    }

    function decodeBase64(base64String) {
        const binaryString = window.atob(base64String);
        const len = binaryString.length;
        const uint8Array = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            uint8Array[i] = binaryString.charCodeAt(i);
        }
        return uint8Array;
    }

    function assert(condition, message) {
        if (!condition) {
            message = message || "Assertion failed";
            if (typeof Error !== "undefined") {
                throw new Error(message);
            }
            throw message;
        }
    }

    let hexTable = null;

    function hexStringToBytes(hexString) {
        ensureHexTable();

        const hex = hexString.trim().replace("0x", "").toLowerCase();
        if (hex.length % 2 !== 0)
            throw "Invalid lenght of hexString";

        let bytes = new Uint8Array(hex.length / 2);

        for (let j = 0; j < hex.length; j = j + 2) {
            bytes[j / 2] = hexTable.indexOf(hex.substr(j, 2));
        }
        return bytes;
    }

    function bytesToHexString(uint8Array) {
        ensureHexTable();
        let hex = "";
        for (let i = 0; i < uint8Array.length; i++) {
            hex += hexTable[uint8Array[i]];
        }
    }

    function ensureHexTable() {
        if (hexTable === null) {

            hexTable = new Array(256);

            for (let i = 0; i <= 255; i++) {
                if (i < 16)
                    table[i] = "0" + i.toString(16);
                else
                    table[i] = i.toString(16);
            }
        }
    }
    return {
        generateKeyPair: function () {
            return generateKeyPair();
        },
        decodeBase64: function (base64String) {
            return decodeBase64(base64String);
        },
        encrypt: function (plainTextUint8Array, serverPublicKey, clientPrivateKey) {
            return encrypt(plainTextUint8Array, serverPublicKey, clientPrivateKey);
        },
        decrypt: function (cipherV2Bytes, serverPublicKey, clientPrivateKey) {
            return decrypt(cipherV2Bytes, serverPublicKey, clientPrivateKey);
        },
        hexStringToBytes: function (hexString) {
            return hexStringToBytes(hexString);
        },
        bytesToHexString: function (uint8Array) {
            return bytesToHexString(uint8Array);
        }

    };
})(window);

/* dependency algorithms (c) authors */

(function (axlsign) {
    'use strict';

    // Curve25519 signatures (and also key agreement)
    // like in the early Axolotl.
    //
    // Written by Dmitry Chestnykh.
    // You can use it under MIT or CC0 license.

    // Curve25519 signatures idea and math by Trevor Perrin
    // https://moderncrypto.org/mail-archive/curves/2014/000205.html

    // Derived from TweetNaCl.js (https://tweetnacl.js.org/)
    // Ported in 2014 by Dmitry Chestnykh and Devi Mandiri.
    // Public domain.
    //
    // Implementation derived from TweetNaCl version 20140427.
    // See for details: http://tweetnacl.cr.yp.to/

    var gf = function (init) {
        var i, r = new Float64Array(16);
        if (init) for (i = 0; i < init.length; i++) r[i] = init[i];
        return r;
    };

    var _0 = new Uint8Array(16);
    var _9 = new Uint8Array(32); _9[0] = 9;

    var gf0 = gf(),
        gf1 = gf([1]),
        _121665 = gf([0xdb41, 1]),
        D = gf([0x78a3, 0x1359, 0x4dca, 0x75eb, 0xd8ab, 0x4141, 0x0a4d, 0x0070, 0xe898, 0x7779, 0x4079, 0x8cc7, 0xfe73, 0x2b6f, 0x6cee, 0x5203]),
        D2 = gf([0xf159, 0x26b2, 0x9b94, 0xebd6, 0xb156, 0x8283, 0x149a, 0x00e0, 0xd130, 0xeef3, 0x80f2, 0x198e, 0xfce7, 0x56df, 0xd9dc, 0x2406]),
        X = gf([0xd51a, 0x8f25, 0x2d60, 0xc956, 0xa7b2, 0x9525, 0xc760, 0x692c, 0xdc5c, 0xfdd6, 0xe231, 0xc0a4, 0x53fe, 0xcd6e, 0x36d3, 0x2169]),
        Y = gf([0x6658, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666, 0x6666]),
        I = gf([0xa0b0, 0x4a0e, 0x1b27, 0xc4ee, 0xe478, 0xad2f, 0x1806, 0x2f43, 0xd7a7, 0x3dfb, 0x0099, 0x2b4d, 0xdf0b, 0x4fc1, 0x2480, 0x2b83]);

    function ts64(x, i, h, l) {
        x[i] = (h >> 24) & 0xff;
        x[i + 1] = (h >> 16) & 0xff;
        x[i + 2] = (h >> 8) & 0xff;
        x[i + 3] = h & 0xff;
        x[i + 4] = (l >> 24) & 0xff;
        x[i + 5] = (l >> 16) & 0xff;
        x[i + 6] = (l >> 8) & 0xff;
        x[i + 7] = l & 0xff;
    }

    function vn(x, xi, y, yi, n) {
        var i, d = 0;
        for (i = 0; i < n; i++) d |= x[xi + i] ^ y[yi + i];
        return (1 & ((d - 1) >>> 8)) - 1;
    }

    function crypto_verify_32(x, xi, y, yi) {
        return vn(x, xi, y, yi, 32);
    }

    function set25519(r, a) {
        var i;
        for (i = 0; i < 16; i++) r[i] = a[i] | 0;
    }

    function car25519(o) {
        var i, v, c = 1;
        for (i = 0; i < 16; i++) {
            v = o[i] + c + 65535;
            c = Math.floor(v / 65536);
            o[i] = v - c * 65536;
        }
        o[0] += c - 1 + 37 * (c - 1);
    }

    function sel25519(p, q, b) {
        var t, c = ~(b - 1);
        for (var i = 0; i < 16; i++) {
            t = c & (p[i] ^ q[i]);
            p[i] ^= t;
            q[i] ^= t;
        }
    }

    function pack25519(o, n) {
        var i, j, b;
        var m = gf(), t = gf();
        for (i = 0; i < 16; i++) t[i] = n[i];
        car25519(t);
        car25519(t);
        car25519(t);
        for (j = 0; j < 2; j++) {
            m[0] = t[0] - 0xffed;
            for (i = 1; i < 15; i++) {
                m[i] = t[i] - 0xffff - ((m[i - 1] >> 16) & 1);
                m[i - 1] &= 0xffff;
            }
            m[15] = t[15] - 0x7fff - ((m[14] >> 16) & 1);
            b = (m[15] >> 16) & 1;
            m[14] &= 0xffff;
            sel25519(t, m, 1 - b);
        }
        for (i = 0; i < 16; i++) {
            o[2 * i] = t[i] & 0xff;
            o[2 * i + 1] = t[i] >> 8;
        }
    }

    function neq25519(a, b) {
        var c = new Uint8Array(32), d = new Uint8Array(32);
        pack25519(c, a);
        pack25519(d, b);
        return crypto_verify_32(c, 0, d, 0);
    }

    function par25519(a) {
        var d = new Uint8Array(32);
        pack25519(d, a);
        return d[0] & 1;
    }

    function unpack25519(o, n) {
        var i;
        for (i = 0; i < 16; i++) o[i] = n[2 * i] + (n[2 * i + 1] << 8);
        o[15] &= 0x7fff;
    }

    function A(o, a, b) {
        for (var i = 0; i < 16; i++) o[i] = a[i] + b[i];
    }

    function Z(o, a, b) {
        for (var i = 0; i < 16; i++) o[i] = a[i] - b[i];
    }

    function M(o, a, b) {
        var v, c,
            t0 = 0, t1 = 0, t2 = 0, t3 = 0, t4 = 0, t5 = 0, t6 = 0, t7 = 0,
            t8 = 0, t9 = 0, t10 = 0, t11 = 0, t12 = 0, t13 = 0, t14 = 0, t15 = 0,
            t16 = 0, t17 = 0, t18 = 0, t19 = 0, t20 = 0, t21 = 0, t22 = 0, t23 = 0,
            t24 = 0, t25 = 0, t26 = 0, t27 = 0, t28 = 0, t29 = 0, t30 = 0,
            b0 = b[0],
            b1 = b[1],
            b2 = b[2],
            b3 = b[3],
            b4 = b[4],
            b5 = b[5],
            b6 = b[6],
            b7 = b[7],
            b8 = b[8],
            b9 = b[9],
            b10 = b[10],
            b11 = b[11],
            b12 = b[12],
            b13 = b[13],
            b14 = b[14],
            b15 = b[15];

        v = a[0];
        t0 += v * b0;
        t1 += v * b1;
        t2 += v * b2;
        t3 += v * b3;
        t4 += v * b4;
        t5 += v * b5;
        t6 += v * b6;
        t7 += v * b7;
        t8 += v * b8;
        t9 += v * b9;
        t10 += v * b10;
        t11 += v * b11;
        t12 += v * b12;
        t13 += v * b13;
        t14 += v * b14;
        t15 += v * b15;
        v = a[1];
        t1 += v * b0;
        t2 += v * b1;
        t3 += v * b2;
        t4 += v * b3;
        t5 += v * b4;
        t6 += v * b5;
        t7 += v * b6;
        t8 += v * b7;
        t9 += v * b8;
        t10 += v * b9;
        t11 += v * b10;
        t12 += v * b11;
        t13 += v * b12;
        t14 += v * b13;
        t15 += v * b14;
        t16 += v * b15;
        v = a[2];
        t2 += v * b0;
        t3 += v * b1;
        t4 += v * b2;
        t5 += v * b3;
        t6 += v * b4;
        t7 += v * b5;
        t8 += v * b6;
        t9 += v * b7;
        t10 += v * b8;
        t11 += v * b9;
        t12 += v * b10;
        t13 += v * b11;
        t14 += v * b12;
        t15 += v * b13;
        t16 += v * b14;
        t17 += v * b15;
        v = a[3];
        t3 += v * b0;
        t4 += v * b1;
        t5 += v * b2;
        t6 += v * b3;
        t7 += v * b4;
        t8 += v * b5;
        t9 += v * b6;
        t10 += v * b7;
        t11 += v * b8;
        t12 += v * b9;
        t13 += v * b10;
        t14 += v * b11;
        t15 += v * b12;
        t16 += v * b13;
        t17 += v * b14;
        t18 += v * b15;
        v = a[4];
        t4 += v * b0;
        t5 += v * b1;
        t6 += v * b2;
        t7 += v * b3;
        t8 += v * b4;
        t9 += v * b5;
        t10 += v * b6;
        t11 += v * b7;
        t12 += v * b8;
        t13 += v * b9;
        t14 += v * b10;
        t15 += v * b11;
        t16 += v * b12;
        t17 += v * b13;
        t18 += v * b14;
        t19 += v * b15;
        v = a[5];
        t5 += v * b0;
        t6 += v * b1;
        t7 += v * b2;
        t8 += v * b3;
        t9 += v * b4;
        t10 += v * b5;
        t11 += v * b6;
        t12 += v * b7;
        t13 += v * b8;
        t14 += v * b9;
        t15 += v * b10;
        t16 += v * b11;
        t17 += v * b12;
        t18 += v * b13;
        t19 += v * b14;
        t20 += v * b15;
        v = a[6];
        t6 += v * b0;
        t7 += v * b1;
        t8 += v * b2;
        t9 += v * b3;
        t10 += v * b4;
        t11 += v * b5;
        t12 += v * b6;
        t13 += v * b7;
        t14 += v * b8;
        t15 += v * b9;
        t16 += v * b10;
        t17 += v * b11;
        t18 += v * b12;
        t19 += v * b13;
        t20 += v * b14;
        t21 += v * b15;
        v = a[7];
        t7 += v * b0;
        t8 += v * b1;
        t9 += v * b2;
        t10 += v * b3;
        t11 += v * b4;
        t12 += v * b5;
        t13 += v * b6;
        t14 += v * b7;
        t15 += v * b8;
        t16 += v * b9;
        t17 += v * b10;
        t18 += v * b11;
        t19 += v * b12;
        t20 += v * b13;
        t21 += v * b14;
        t22 += v * b15;
        v = a[8];
        t8 += v * b0;
        t9 += v * b1;
        t10 += v * b2;
        t11 += v * b3;
        t12 += v * b4;
        t13 += v * b5;
        t14 += v * b6;
        t15 += v * b7;
        t16 += v * b8;
        t17 += v * b9;
        t18 += v * b10;
        t19 += v * b11;
        t20 += v * b12;
        t21 += v * b13;
        t22 += v * b14;
        t23 += v * b15;
        v = a[9];
        t9 += v * b0;
        t10 += v * b1;
        t11 += v * b2;
        t12 += v * b3;
        t13 += v * b4;
        t14 += v * b5;
        t15 += v * b6;
        t16 += v * b7;
        t17 += v * b8;
        t18 += v * b9;
        t19 += v * b10;
        t20 += v * b11;
        t21 += v * b12;
        t22 += v * b13;
        t23 += v * b14;
        t24 += v * b15;
        v = a[10];
        t10 += v * b0;
        t11 += v * b1;
        t12 += v * b2;
        t13 += v * b3;
        t14 += v * b4;
        t15 += v * b5;
        t16 += v * b6;
        t17 += v * b7;
        t18 += v * b8;
        t19 += v * b9;
        t20 += v * b10;
        t21 += v * b11;
        t22 += v * b12;
        t23 += v * b13;
        t24 += v * b14;
        t25 += v * b15;
        v = a[11];
        t11 += v * b0;
        t12 += v * b1;
        t13 += v * b2;
        t14 += v * b3;
        t15 += v * b4;
        t16 += v * b5;
        t17 += v * b6;
        t18 += v * b7;
        t19 += v * b8;
        t20 += v * b9;
        t21 += v * b10;
        t22 += v * b11;
        t23 += v * b12;
        t24 += v * b13;
        t25 += v * b14;
        t26 += v * b15;
        v = a[12];
        t12 += v * b0;
        t13 += v * b1;
        t14 += v * b2;
        t15 += v * b3;
        t16 += v * b4;
        t17 += v * b5;
        t18 += v * b6;
        t19 += v * b7;
        t20 += v * b8;
        t21 += v * b9;
        t22 += v * b10;
        t23 += v * b11;
        t24 += v * b12;
        t25 += v * b13;
        t26 += v * b14;
        t27 += v * b15;
        v = a[13];
        t13 += v * b0;
        t14 += v * b1;
        t15 += v * b2;
        t16 += v * b3;
        t17 += v * b4;
        t18 += v * b5;
        t19 += v * b6;
        t20 += v * b7;
        t21 += v * b8;
        t22 += v * b9;
        t23 += v * b10;
        t24 += v * b11;
        t25 += v * b12;
        t26 += v * b13;
        t27 += v * b14;
        t28 += v * b15;
        v = a[14];
        t14 += v * b0;
        t15 += v * b1;
        t16 += v * b2;
        t17 += v * b3;
        t18 += v * b4;
        t19 += v * b5;
        t20 += v * b6;
        t21 += v * b7;
        t22 += v * b8;
        t23 += v * b9;
        t24 += v * b10;
        t25 += v * b11;
        t26 += v * b12;
        t27 += v * b13;
        t28 += v * b14;
        t29 += v * b15;
        v = a[15];
        t15 += v * b0;
        t16 += v * b1;
        t17 += v * b2;
        t18 += v * b3;
        t19 += v * b4;
        t20 += v * b5;
        t21 += v * b6;
        t22 += v * b7;
        t23 += v * b8;
        t24 += v * b9;
        t25 += v * b10;
        t26 += v * b11;
        t27 += v * b12;
        t28 += v * b13;
        t29 += v * b14;
        t30 += v * b15;

        t0 += 38 * t16;
        t1 += 38 * t17;
        t2 += 38 * t18;
        t3 += 38 * t19;
        t4 += 38 * t20;
        t5 += 38 * t21;
        t6 += 38 * t22;
        t7 += 38 * t23;
        t8 += 38 * t24;
        t9 += 38 * t25;
        t10 += 38 * t26;
        t11 += 38 * t27;
        t12 += 38 * t28;
        t13 += 38 * t29;
        t14 += 38 * t30;
        // t15 left as is

        // first car
        c = 1;
        v = t0 + c + 65535; c = Math.floor(v / 65536); t0 = v - c * 65536;
        v = t1 + c + 65535; c = Math.floor(v / 65536); t1 = v - c * 65536;
        v = t2 + c + 65535; c = Math.floor(v / 65536); t2 = v - c * 65536;
        v = t3 + c + 65535; c = Math.floor(v / 65536); t3 = v - c * 65536;
        v = t4 + c + 65535; c = Math.floor(v / 65536); t4 = v - c * 65536;
        v = t5 + c + 65535; c = Math.floor(v / 65536); t5 = v - c * 65536;
        v = t6 + c + 65535; c = Math.floor(v / 65536); t6 = v - c * 65536;
        v = t7 + c + 65535; c = Math.floor(v / 65536); t7 = v - c * 65536;
        v = t8 + c + 65535; c = Math.floor(v / 65536); t8 = v - c * 65536;
        v = t9 + c + 65535; c = Math.floor(v / 65536); t9 = v - c * 65536;
        v = t10 + c + 65535; c = Math.floor(v / 65536); t10 = v - c * 65536;
        v = t11 + c + 65535; c = Math.floor(v / 65536); t11 = v - c * 65536;
        v = t12 + c + 65535; c = Math.floor(v / 65536); t12 = v - c * 65536;
        v = t13 + c + 65535; c = Math.floor(v / 65536); t13 = v - c * 65536;
        v = t14 + c + 65535; c = Math.floor(v / 65536); t14 = v - c * 65536;
        v = t15 + c + 65535; c = Math.floor(v / 65536); t15 = v - c * 65536;
        t0 += c - 1 + 37 * (c - 1);

        // second car
        c = 1;
        v = t0 + c + 65535; c = Math.floor(v / 65536); t0 = v - c * 65536;
        v = t1 + c + 65535; c = Math.floor(v / 65536); t1 = v - c * 65536;
        v = t2 + c + 65535; c = Math.floor(v / 65536); t2 = v - c * 65536;
        v = t3 + c + 65535; c = Math.floor(v / 65536); t3 = v - c * 65536;
        v = t4 + c + 65535; c = Math.floor(v / 65536); t4 = v - c * 65536;
        v = t5 + c + 65535; c = Math.floor(v / 65536); t5 = v - c * 65536;
        v = t6 + c + 65535; c = Math.floor(v / 65536); t6 = v - c * 65536;
        v = t7 + c + 65535; c = Math.floor(v / 65536); t7 = v - c * 65536;
        v = t8 + c + 65535; c = Math.floor(v / 65536); t8 = v - c * 65536;
        v = t9 + c + 65535; c = Math.floor(v / 65536); t9 = v - c * 65536;
        v = t10 + c + 65535; c = Math.floor(v / 65536); t10 = v - c * 65536;
        v = t11 + c + 65535; c = Math.floor(v / 65536); t11 = v - c * 65536;
        v = t12 + c + 65535; c = Math.floor(v / 65536); t12 = v - c * 65536;
        v = t13 + c + 65535; c = Math.floor(v / 65536); t13 = v - c * 65536;
        v = t14 + c + 65535; c = Math.floor(v / 65536); t14 = v - c * 65536;
        v = t15 + c + 65535; c = Math.floor(v / 65536); t15 = v - c * 65536;
        t0 += c - 1 + 37 * (c - 1);

        o[0] = t0;
        o[1] = t1;
        o[2] = t2;
        o[3] = t3;
        o[4] = t4;
        o[5] = t5;
        o[6] = t6;
        o[7] = t7;
        o[8] = t8;
        o[9] = t9;
        o[10] = t10;
        o[11] = t11;
        o[12] = t12;
        o[13] = t13;
        o[14] = t14;
        o[15] = t15;
    }

    function S(o, a) {
        M(o, a, a);
    }

    function inv25519(o, i) {
        var c = gf();
        var a;
        for (a = 0; a < 16; a++) c[a] = i[a];
        for (a = 253; a >= 0; a--) {
            S(c, c);
            if (a !== 2 && a !== 4) M(c, c, i);
        }
        for (a = 0; a < 16; a++) o[a] = c[a];
    }

    function pow2523(o, i) {
        var c = gf();
        var a;
        for (a = 0; a < 16; a++) c[a] = i[a];
        for (a = 250; a >= 0; a--) {
            S(c, c);
            if (a !== 1) M(c, c, i);
        }
        for (a = 0; a < 16; a++) o[a] = c[a];
    }

    function crypto_scalarmult(q, n, p) {
        var z = new Uint8Array(32);
        var x = new Float64Array(80), r, i;
        var a = gf(), b = gf(), c = gf(),
            d = gf(), e = gf(), f = gf();
        for (i = 0; i < 31; i++) z[i] = n[i];
        z[31] = (n[31] & 127) | 64;
        z[0] &= 248;
        unpack25519(x, p);
        for (i = 0; i < 16; i++) {
            b[i] = x[i];
            d[i] = a[i] = c[i] = 0;
        }
        a[0] = d[0] = 1;
        for (i = 254; i >= 0; --i) {
            r = (z[i >>> 3] >>> (i & 7)) & 1;
            sel25519(a, b, r);
            sel25519(c, d, r);
            A(e, a, c);
            Z(a, a, c);
            A(c, b, d);
            Z(b, b, d);
            S(d, e);
            S(f, a);
            M(a, c, a);
            M(c, b, e);
            A(e, a, c);
            Z(a, a, c);
            S(b, a);
            Z(c, d, f);
            M(a, c, _121665);
            A(a, a, d);
            M(c, c, a);
            M(a, d, f);
            M(d, b, x);
            S(b, e);
            sel25519(a, b, r);
            sel25519(c, d, r);
        }
        for (i = 0; i < 16; i++) {
            x[i + 16] = a[i];
            x[i + 32] = c[i];
            x[i + 48] = b[i];
            x[i + 64] = d[i];
        }
        var x32 = x.subarray(32);
        var x16 = x.subarray(16);
        inv25519(x32, x32);
        M(x16, x16, x32);
        pack25519(q, x16);
        return 0;
    }

    function crypto_scalarmult_base(q, n) {
        return crypto_scalarmult(q, n, _9);
    }

    var K = [
        0x428a2f98, 0xd728ae22, 0x71374491, 0x23ef65cd,
        0xb5c0fbcf, 0xec4d3b2f, 0xe9b5dba5, 0x8189dbbc,
        0x3956c25b, 0xf348b538, 0x59f111f1, 0xb605d019,
        0x923f82a4, 0xaf194f9b, 0xab1c5ed5, 0xda6d8118,
        0xd807aa98, 0xa3030242, 0x12835b01, 0x45706fbe,
        0x243185be, 0x4ee4b28c, 0x550c7dc3, 0xd5ffb4e2,
        0x72be5d74, 0xf27b896f, 0x80deb1fe, 0x3b1696b1,
        0x9bdc06a7, 0x25c71235, 0xc19bf174, 0xcf692694,
        0xe49b69c1, 0x9ef14ad2, 0xefbe4786, 0x384f25e3,
        0x0fc19dc6, 0x8b8cd5b5, 0x240ca1cc, 0x77ac9c65,
        0x2de92c6f, 0x592b0275, 0x4a7484aa, 0x6ea6e483,
        0x5cb0a9dc, 0xbd41fbd4, 0x76f988da, 0x831153b5,
        0x983e5152, 0xee66dfab, 0xa831c66d, 0x2db43210,
        0xb00327c8, 0x98fb213f, 0xbf597fc7, 0xbeef0ee4,
        0xc6e00bf3, 0x3da88fc2, 0xd5a79147, 0x930aa725,
        0x06ca6351, 0xe003826f, 0x14292967, 0x0a0e6e70,
        0x27b70a85, 0x46d22ffc, 0x2e1b2138, 0x5c26c926,
        0x4d2c6dfc, 0x5ac42aed, 0x53380d13, 0x9d95b3df,
        0x650a7354, 0x8baf63de, 0x766a0abb, 0x3c77b2a8,
        0x81c2c92e, 0x47edaee6, 0x92722c85, 0x1482353b,
        0xa2bfe8a1, 0x4cf10364, 0xa81a664b, 0xbc423001,
        0xc24b8b70, 0xd0f89791, 0xc76c51a3, 0x0654be30,
        0xd192e819, 0xd6ef5218, 0xd6990624, 0x5565a910,
        0xf40e3585, 0x5771202a, 0x106aa070, 0x32bbd1b8,
        0x19a4c116, 0xb8d2d0c8, 0x1e376c08, 0x5141ab53,
        0x2748774c, 0xdf8eeb99, 0x34b0bcb5, 0xe19b48a8,
        0x391c0cb3, 0xc5c95a63, 0x4ed8aa4a, 0xe3418acb,
        0x5b9cca4f, 0x7763e373, 0x682e6ff3, 0xd6b2b8a3,
        0x748f82ee, 0x5defb2fc, 0x78a5636f, 0x43172f60,
        0x84c87814, 0xa1f0ab72, 0x8cc70208, 0x1a6439ec,
        0x90befffa, 0x23631e28, 0xa4506ceb, 0xde82bde9,
        0xbef9a3f7, 0xb2c67915, 0xc67178f2, 0xe372532b,
        0xca273ece, 0xea26619c, 0xd186b8c7, 0x21c0c207,
        0xeada7dd6, 0xcde0eb1e, 0xf57d4f7f, 0xee6ed178,
        0x06f067aa, 0x72176fba, 0x0a637dc5, 0xa2c898a6,
        0x113f9804, 0xbef90dae, 0x1b710b35, 0x131c471b,
        0x28db77f5, 0x23047d84, 0x32caab7b, 0x40c72493,
        0x3c9ebe0a, 0x15c9bebc, 0x431d67c4, 0x9c100d4c,
        0x4cc5d4be, 0xcb3e42b6, 0x597f299c, 0xfc657e2a,
        0x5fcb6fab, 0x3ad6faec, 0x6c44198c, 0x4a475817
    ];

    function crypto_hashblocks_hl(hh, hl, m, n) {
        var wh = new Int32Array(16), wl = new Int32Array(16),
            bh0, bh1, bh2, bh3, bh4, bh5, bh6, bh7,
            bl0, bl1, bl2, bl3, bl4, bl5, bl6, bl7,
            th, tl, i, j, h, l, a, b, c, d;

        var ah0 = hh[0],
            ah1 = hh[1],
            ah2 = hh[2],
            ah3 = hh[3],
            ah4 = hh[4],
            ah5 = hh[5],
            ah6 = hh[6],
            ah7 = hh[7],

            al0 = hl[0],
            al1 = hl[1],
            al2 = hl[2],
            al3 = hl[3],
            al4 = hl[4],
            al5 = hl[5],
            al6 = hl[6],
            al7 = hl[7];

        var pos = 0;
        while (n >= 128) {
            for (i = 0; i < 16; i++) {
                j = 8 * i + pos;
                wh[i] = (m[j + 0] << 24) | (m[j + 1] << 16) | (m[j + 2] << 8) | m[j + 3];
                wl[i] = (m[j + 4] << 24) | (m[j + 5] << 16) | (m[j + 6] << 8) | m[j + 7];
            }
            for (i = 0; i < 80; i++) {
                bh0 = ah0;
                bh1 = ah1;
                bh2 = ah2;
                bh3 = ah3;
                bh4 = ah4;
                bh5 = ah5;
                bh6 = ah6;
                bh7 = ah7;

                bl0 = al0;
                bl1 = al1;
                bl2 = al2;
                bl3 = al3;
                bl4 = al4;
                bl5 = al5;
                bl6 = al6;
                bl7 = al7;

                // add
                h = ah7;
                l = al7;

                a = l & 0xffff; b = l >>> 16;
                c = h & 0xffff; d = h >>> 16;

                // Sigma1
                h = ((ah4 >>> 14) | (al4 << (32 - 14))) ^ ((ah4 >>> 18) | (al4 << (32 - 18))) ^ ((al4 >>> (41 - 32)) | (ah4 << (32 - (41 - 32))));
                l = ((al4 >>> 14) | (ah4 << (32 - 14))) ^ ((al4 >>> 18) | (ah4 << (32 - 18))) ^ ((ah4 >>> (41 - 32)) | (al4 << (32 - (41 - 32))));

                a += l & 0xffff; b += l >>> 16;
                c += h & 0xffff; d += h >>> 16;

                // Ch
                h = (ah4 & ah5) ^ (~ah4 & ah6);
                l = (al4 & al5) ^ (~al4 & al6);

                a += l & 0xffff; b += l >>> 16;
                c += h & 0xffff; d += h >>> 16;

                // K
                h = K[i * 2];
                l = K[i * 2 + 1];

                a += l & 0xffff; b += l >>> 16;
                c += h & 0xffff; d += h >>> 16;

                // w
                h = wh[i % 16];
                l = wl[i % 16];

                a += l & 0xffff; b += l >>> 16;
                c += h & 0xffff; d += h >>> 16;

                b += a >>> 16;
                c += b >>> 16;
                d += c >>> 16;

                th = c & 0xffff | d << 16;
                tl = a & 0xffff | b << 16;

                // add
                h = th;
                l = tl;

                a = l & 0xffff; b = l >>> 16;
                c = h & 0xffff; d = h >>> 16;

                // Sigma0
                h = ((ah0 >>> 28) | (al0 << (32 - 28))) ^ ((al0 >>> (34 - 32)) | (ah0 << (32 - (34 - 32)))) ^ ((al0 >>> (39 - 32)) | (ah0 << (32 - (39 - 32))));
                l = ((al0 >>> 28) | (ah0 << (32 - 28))) ^ ((ah0 >>> (34 - 32)) | (al0 << (32 - (34 - 32)))) ^ ((ah0 >>> (39 - 32)) | (al0 << (32 - (39 - 32))));

                a += l & 0xffff; b += l >>> 16;
                c += h & 0xffff; d += h >>> 16;

                // Maj
                h = (ah0 & ah1) ^ (ah0 & ah2) ^ (ah1 & ah2);
                l = (al0 & al1) ^ (al0 & al2) ^ (al1 & al2);

                a += l & 0xffff; b += l >>> 16;
                c += h & 0xffff; d += h >>> 16;

                b += a >>> 16;
                c += b >>> 16;
                d += c >>> 16;

                bh7 = (c & 0xffff) | (d << 16);
                bl7 = (a & 0xffff) | (b << 16);

                // add
                h = bh3;
                l = bl3;

                a = l & 0xffff; b = l >>> 16;
                c = h & 0xffff; d = h >>> 16;

                h = th;
                l = tl;

                a += l & 0xffff; b += l >>> 16;
                c += h & 0xffff; d += h >>> 16;

                b += a >>> 16;
                c += b >>> 16;
                d += c >>> 16;

                bh3 = (c & 0xffff) | (d << 16);
                bl3 = (a & 0xffff) | (b << 16);

                ah1 = bh0;
                ah2 = bh1;
                ah3 = bh2;
                ah4 = bh3;
                ah5 = bh4;
                ah6 = bh5;
                ah7 = bh6;
                ah0 = bh7;

                al1 = bl0;
                al2 = bl1;
                al3 = bl2;
                al4 = bl3;
                al5 = bl4;
                al6 = bl5;
                al7 = bl6;
                al0 = bl7;

                if (i % 16 === 15) {
                    for (j = 0; j < 16; j++) {
                        // add
                        h = wh[j];
                        l = wl[j];

                        a = l & 0xffff; b = l >>> 16;
                        c = h & 0xffff; d = h >>> 16;

                        h = wh[(j + 9) % 16];
                        l = wl[(j + 9) % 16];

                        a += l & 0xffff; b += l >>> 16;
                        c += h & 0xffff; d += h >>> 16;

                        // sigma0
                        th = wh[(j + 1) % 16];
                        tl = wl[(j + 1) % 16];
                        h = ((th >>> 1) | (tl << (32 - 1))) ^ ((th >>> 8) | (tl << (32 - 8))) ^ (th >>> 7);
                        l = ((tl >>> 1) | (th << (32 - 1))) ^ ((tl >>> 8) | (th << (32 - 8))) ^ ((tl >>> 7) | (th << (32 - 7)));

                        a += l & 0xffff; b += l >>> 16;
                        c += h & 0xffff; d += h >>> 16;

                        // sigma1
                        th = wh[(j + 14) % 16];
                        tl = wl[(j + 14) % 16];
                        h = ((th >>> 19) | (tl << (32 - 19))) ^ ((tl >>> (61 - 32)) | (th << (32 - (61 - 32)))) ^ (th >>> 6);
                        l = ((tl >>> 19) | (th << (32 - 19))) ^ ((th >>> (61 - 32)) | (tl << (32 - (61 - 32)))) ^ ((tl >>> 6) | (th << (32 - 6)));

                        a += l & 0xffff; b += l >>> 16;
                        c += h & 0xffff; d += h >>> 16;

                        b += a >>> 16;
                        c += b >>> 16;
                        d += c >>> 16;

                        wh[j] = (c & 0xffff) | (d << 16);
                        wl[j] = (a & 0xffff) | (b << 16);
                    }
                }
            }

            // add
            h = ah0;
            l = al0;

            a = l & 0xffff; b = l >>> 16;
            c = h & 0xffff; d = h >>> 16;

            h = hh[0];
            l = hl[0];

            a += l & 0xffff; b += l >>> 16;
            c += h & 0xffff; d += h >>> 16;

            b += a >>> 16;
            c += b >>> 16;
            d += c >>> 16;

            hh[0] = ah0 = (c & 0xffff) | (d << 16);
            hl[0] = al0 = (a & 0xffff) | (b << 16);

            h = ah1;
            l = al1;

            a = l & 0xffff; b = l >>> 16;
            c = h & 0xffff; d = h >>> 16;

            h = hh[1];
            l = hl[1];

            a += l & 0xffff; b += l >>> 16;
            c += h & 0xffff; d += h >>> 16;

            b += a >>> 16;
            c += b >>> 16;
            d += c >>> 16;

            hh[1] = ah1 = (c & 0xffff) | (d << 16);
            hl[1] = al1 = (a & 0xffff) | (b << 16);

            h = ah2;
            l = al2;

            a = l & 0xffff; b = l >>> 16;
            c = h & 0xffff; d = h >>> 16;

            h = hh[2];
            l = hl[2];

            a += l & 0xffff; b += l >>> 16;
            c += h & 0xffff; d += h >>> 16;

            b += a >>> 16;
            c += b >>> 16;
            d += c >>> 16;

            hh[2] = ah2 = (c & 0xffff) | (d << 16);
            hl[2] = al2 = (a & 0xffff) | (b << 16);

            h = ah3;
            l = al3;

            a = l & 0xffff; b = l >>> 16;
            c = h & 0xffff; d = h >>> 16;

            h = hh[3];
            l = hl[3];

            a += l & 0xffff; b += l >>> 16;
            c += h & 0xffff; d += h >>> 16;

            b += a >>> 16;
            c += b >>> 16;
            d += c >>> 16;

            hh[3] = ah3 = (c & 0xffff) | (d << 16);
            hl[3] = al3 = (a & 0xffff) | (b << 16);

            h = ah4;
            l = al4;

            a = l & 0xffff; b = l >>> 16;
            c = h & 0xffff; d = h >>> 16;

            h = hh[4];
            l = hl[4];

            a += l & 0xffff; b += l >>> 16;
            c += h & 0xffff; d += h >>> 16;

            b += a >>> 16;
            c += b >>> 16;
            d += c >>> 16;

            hh[4] = ah4 = (c & 0xffff) | (d << 16);
            hl[4] = al4 = (a & 0xffff) | (b << 16);

            h = ah5;
            l = al5;

            a = l & 0xffff; b = l >>> 16;
            c = h & 0xffff; d = h >>> 16;

            h = hh[5];
            l = hl[5];

            a += l & 0xffff; b += l >>> 16;
            c += h & 0xffff; d += h >>> 16;

            b += a >>> 16;
            c += b >>> 16;
            d += c >>> 16;

            hh[5] = ah5 = (c & 0xffff) | (d << 16);
            hl[5] = al5 = (a & 0xffff) | (b << 16);

            h = ah6;
            l = al6;

            a = l & 0xffff; b = l >>> 16;
            c = h & 0xffff; d = h >>> 16;

            h = hh[6];
            l = hl[6];

            a += l & 0xffff; b += l >>> 16;
            c += h & 0xffff; d += h >>> 16;

            b += a >>> 16;
            c += b >>> 16;
            d += c >>> 16;

            hh[6] = ah6 = (c & 0xffff) | (d << 16);
            hl[6] = al6 = (a & 0xffff) | (b << 16);

            h = ah7;
            l = al7;

            a = l & 0xffff; b = l >>> 16;
            c = h & 0xffff; d = h >>> 16;

            h = hh[7];
            l = hl[7];

            a += l & 0xffff; b += l >>> 16;
            c += h & 0xffff; d += h >>> 16;

            b += a >>> 16;
            c += b >>> 16;
            d += c >>> 16;

            hh[7] = ah7 = (c & 0xffff) | (d << 16);
            hl[7] = al7 = (a & 0xffff) | (b << 16);

            pos += 128;
            n -= 128;
        }

        return n;
    }

    function crypto_hash(out, m, n) {
        var hh = new Int32Array(8),
            hl = new Int32Array(8),
            x = new Uint8Array(256),
            i, b = n;

        hh[0] = 0x6a09e667;
        hh[1] = 0xbb67ae85;
        hh[2] = 0x3c6ef372;
        hh[3] = 0xa54ff53a;
        hh[4] = 0x510e527f;
        hh[5] = 0x9b05688c;
        hh[6] = 0x1f83d9ab;
        hh[7] = 0x5be0cd19;

        hl[0] = 0xf3bcc908;
        hl[1] = 0x84caa73b;
        hl[2] = 0xfe94f82b;
        hl[3] = 0x5f1d36f1;
        hl[4] = 0xade682d1;
        hl[5] = 0x2b3e6c1f;
        hl[6] = 0xfb41bd6b;
        hl[7] = 0x137e2179;

        crypto_hashblocks_hl(hh, hl, m, n);
        n %= 128;

        for (i = 0; i < n; i++) x[i] = m[b - n + i];
        x[n] = 128;

        n = 256 - 128 * (n < 112 ? 1 : 0);
        x[n - 9] = 0;
        ts64(x, n - 8, (b / 0x20000000) | 0, b << 3);
        crypto_hashblocks_hl(hh, hl, x, n);

        for (i = 0; i < 8; i++) ts64(out, 8 * i, hh[i], hl[i]);

        return 0;
    }

    function add(p, q) {
        var a = gf(), b = gf(), c = gf(),
            d = gf(), e = gf(), f = gf(),
            g = gf(), h = gf(), t = gf();

        Z(a, p[1], p[0]);
        Z(t, q[1], q[0]);
        M(a, a, t);
        A(b, p[0], p[1]);
        A(t, q[0], q[1]);
        M(b, b, t);
        M(c, p[3], q[3]);
        M(c, c, D2);
        M(d, p[2], q[2]);
        A(d, d, d);
        Z(e, b, a);
        Z(f, d, c);
        A(g, d, c);
        A(h, b, a);

        M(p[0], e, f);
        M(p[1], h, g);
        M(p[2], g, f);
        M(p[3], e, h);
    }

    function cswap(p, q, b) {
        var i;
        for (i = 0; i < 4; i++) {
            sel25519(p[i], q[i], b);
        }
    }

    function pack(r, p) {
        var tx = gf(), ty = gf(), zi = gf();
        inv25519(zi, p[2]);
        M(tx, p[0], zi);
        M(ty, p[1], zi);
        pack25519(r, ty);
        r[31] ^= par25519(tx) << 7;
    }

    function scalarmult(p, q, s) {
        var b, i;
        set25519(p[0], gf0);
        set25519(p[1], gf1);
        set25519(p[2], gf1);
        set25519(p[3], gf0);
        for (i = 255; i >= 0; --i) {
            b = (s[(i / 8) | 0] >> (i & 7)) & 1;
            cswap(p, q, b);
            add(q, p);
            add(p, p);
            cswap(p, q, b);
        }
    }

    function scalarbase(p, s) {
        var q = [gf(), gf(), gf(), gf()];
        set25519(q[0], X);
        set25519(q[1], Y);
        set25519(q[2], gf1);
        M(q[3], X, Y);
        scalarmult(p, q, s);
    }

    var L = new Float64Array([0xed, 0xd3, 0xf5, 0x5c, 0x1a, 0x63, 0x12, 0x58, 0xd6, 0x9c, 0xf7, 0xa2, 0xde, 0xf9, 0xde, 0x14, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x10]);

    function modL(r, x) {
        var carry, i, j, k;
        for (i = 63; i >= 32; --i) {
            carry = 0;
            for (j = i - 32, k = i - 12; j < k; ++j) {
                x[j] += carry - 16 * x[i] * L[j - (i - 32)];
                carry = (x[j] + 128) >> 8;
                x[j] -= carry * 256;
            }
            x[j] += carry;
            x[i] = 0;
        }
        carry = 0;
        for (j = 0; j < 32; j++) {
            x[j] += carry - (x[31] >> 4) * L[j];
            carry = x[j] >> 8;
            x[j] &= 255;
        }
        for (j = 0; j < 32; j++) x[j] -= carry * L[j];
        for (i = 0; i < 32; i++) {
            x[i + 1] += x[i] >> 8;
            r[i] = x[i] & 255;
        }
    }

    function reduce(r) {
        var x = new Float64Array(64), i;
        for (i = 0; i < 64; i++) x[i] = r[i];
        for (i = 0; i < 64; i++) r[i] = 0;
        modL(r, x);
    }

    // Like crypto_sign, but uses secret key directly in hash.
    function crypto_sign_direct(sm, m, n, sk) {
        var d = new Uint8Array(64), h = new Uint8Array(64), r = new Uint8Array(64);
        var i, j, x = new Float64Array(64);
        var p = [gf(), gf(), gf(), gf()];

        for (i = 0; i < n; i++) sm[64 + i] = m[i];
        for (i = 0; i < 32; i++) sm[32 + i] = sk[i];

        crypto_hash(r, sm.subarray(32), n + 32);
        reduce(r);
        scalarbase(p, r);
        pack(sm, p);

        for (i = 0; i < 32; i++) sm[i + 32] = sk[32 + i];
        crypto_hash(h, sm, n + 64);
        reduce(h);

        for (i = 0; i < 64; i++) x[i] = 0;
        for (i = 0; i < 32; i++) x[i] = r[i];
        for (i = 0; i < 32; i++) {
            for (j = 0; j < 32; j++) {
                x[i + j] += h[i] * sk[j];
            }
        }

        modL(sm.subarray(32), x);
        return n + 64;
    }

    // Note: sm must be n+128.
    function crypto_sign_direct_rnd(sm, m, n, sk, rnd) {
        var d = new Uint8Array(64), h = new Uint8Array(64), r = new Uint8Array(64);
        var i, j, x = new Float64Array(64);
        var p = [gf(), gf(), gf(), gf()];

        // Hash separation.
        sm[0] = 0xfe;
        for (i = 1; i < 32; i++) sm[i] = 0xff;

        // Secret key.
        for (i = 0; i < 32; i++) sm[32 + i] = sk[i];

        // Message.
        for (i = 0; i < n; i++) sm[64 + i] = m[i];

        // Random suffix.
        for (i = 0; i < 64; i++) sm[n + 64 + i] = rnd[i];

        crypto_hash(r, sm, n + 128);
        reduce(r);
        scalarbase(p, r);
        pack(sm, p);

        for (i = 0; i < 32; i++) sm[i + 32] = sk[32 + i];
        crypto_hash(h, sm, n + 64);
        reduce(h);

        // Wipe out random suffix.
        for (i = 0; i < 64; i++) sm[n + 64 + i] = 0;

        for (i = 0; i < 64; i++) x[i] = 0;
        for (i = 0; i < 32; i++) x[i] = r[i];
        for (i = 0; i < 32; i++) {
            for (j = 0; j < 32; j++) {
                x[i + j] += h[i] * sk[j];
            }
        }

        modL(sm.subarray(32, n + 64), x);

        return n + 64;
    }


    function curve25519_sign(sm, m, n, sk, opt_rnd) {
        // If opt_rnd is provided, sm must have n + 128,
        // otherwise it must have n + 64 bytes.

        // Convert Curve25519 secret key into Ed25519 secret key (includes pub key).
        var edsk = new Uint8Array(64);
        var p = [gf(), gf(), gf(), gf()];

        for (var i = 0; i < 32; i++) edsk[i] = sk[i];
        // Ensure private key is in the correct format.
        edsk[0] &= 248;
        edsk[31] &= 127;
        edsk[31] |= 64;

        scalarbase(p, edsk);
        pack(edsk.subarray(32), p);

        // Remember sign bit.
        var signBit = edsk[63] & 128;
        var smlen;

        if (opt_rnd) {
            smlen = crypto_sign_direct_rnd(sm, m, n, edsk, opt_rnd);
        } else {
            smlen = crypto_sign_direct(sm, m, n, edsk);
        }

        // Copy sign bit from public key into signature.
        sm[63] |= signBit;
        return smlen;
    }

    function unpackneg(r, p) {
        var t = gf(), chk = gf(), num = gf(),
            den = gf(), den2 = gf(), den4 = gf(),
            den6 = gf();

        set25519(r[2], gf1);
        unpack25519(r[1], p);
        S(num, r[1]);
        M(den, num, D);
        Z(num, num, r[2]);
        A(den, r[2], den);

        S(den2, den);
        S(den4, den2);
        M(den6, den4, den2);
        M(t, den6, num);
        M(t, t, den);

        pow2523(t, t);
        M(t, t, num);
        M(t, t, den);
        M(t, t, den);
        M(r[0], t, den);

        S(chk, r[0]);
        M(chk, chk, den);
        if (neq25519(chk, num)) M(r[0], r[0], I);

        S(chk, r[0]);
        M(chk, chk, den);
        if (neq25519(chk, num)) return -1;

        if (par25519(r[0]) === (p[31] >> 7)) Z(r[0], gf0, r[0]);

        M(r[3], r[0], r[1]);
        return 0;
    }

    function crypto_sign_open(m, sm, n, pk) {
        var i, mlen;
        var t = new Uint8Array(32), h = new Uint8Array(64);
        var p = [gf(), gf(), gf(), gf()],
            q = [gf(), gf(), gf(), gf()];

        mlen = -1;
        if (n < 64) return -1;

        if (unpackneg(q, pk)) return -1;

        for (i = 0; i < n; i++) m[i] = sm[i];
        for (i = 0; i < 32; i++) m[i + 32] = pk[i];
        crypto_hash(h, m, n);
        reduce(h);
        scalarmult(p, q, h);

        scalarbase(q, sm.subarray(32));
        add(p, q);
        pack(t, p);

        n -= 64;
        if (crypto_verify_32(sm, 0, t, 0)) {
            for (i = 0; i < n; i++) m[i] = 0;
            return -1;
        }

        for (i = 0; i < n; i++) m[i] = sm[i + 64];
        mlen = n;
        return mlen;
    }

    // Converts Curve25519 public key back to Ed25519 public key.
    // edwardsY = (montgomeryX - 1) / (montgomeryX + 1)
    function convertPublicKey(pk) {
        var z = new Uint8Array(32),
            x = gf(), a = gf(), b = gf();

        unpack25519(x, pk);

        A(a, x, gf1);
        Z(b, x, gf1);
        inv25519(a, a);
        M(a, a, b);

        pack25519(z, a);
        return z;
    }

    function curve25519_sign_open(m, sm, n, pk) {
        // Convert Curve25519 public key into Ed25519 public key.
        var edpk = convertPublicKey(pk);

        // Restore sign bit from signature.
        edpk[31] |= sm[63] & 128;

        // Remove sign bit from signature.
        sm[63] &= 127;

        // Verify signed message.
        return crypto_sign_open(m, sm, n, edpk);
    }

    /* High-level API */

    function checkArrayTypes() {
        var t, i;
        for (i = 0; i < arguments.length; i++) {
            if ((t = Object.prototype.toString.call(arguments[i])) !== '[object Uint8Array]')
                throw new TypeError('unexpected type ' + t + ', use Uint8Array');
        }
    }

    axlsign.sharedKey = function (secretKey, publicKey) {
        checkArrayTypes(publicKey, secretKey);
        if (publicKey.length !== 32) throw new Error('wrong public key length');
        if (secretKey.length !== 32) throw new Error('wrong secret key length');
        var sharedKey = new Uint8Array(32);
        crypto_scalarmult(sharedKey, secretKey, publicKey);
        return sharedKey;
    };

    axlsign.signMessage = function (secretKey, msg, opt_random) {
        checkArrayTypes(msg, secretKey);
        if (secretKey.length !== 32) throw new Error('wrong secret key length');
        if (opt_random) {
            checkArrayTypes(opt_random)
            if (opt_random.length !== 64) throw new Error('wrong random data length');
            var buf = new Uint8Array(128 + msg.length);
            curve25519_sign(buf, msg, msg.length, secretKey, opt_random);
            return new Uint8Array(buf.subarray(0, 64 + msg.length));
        } else {
            var signedMsg = new Uint8Array(64 + msg.length);
            curve25519_sign(signedMsg, msg, msg.length, secretKey);
            return signedMsg;
        }
    }

    axlsign.openMessage = function (publicKey, signedMsg) {
        checkArrayTypes(signedMsg, publicKey);
        if (publicKey.length !== 32) throw new Error('wrong public key length');
        var tmp = new Uint8Array(signedMsg.length);
        var mlen = curve25519_sign_open(tmp, signedMsg, signedMsg.length, publicKey);
        if (mlen < 0) return null;
        var m = new Uint8Array(mlen);
        for (var i = 0; i < m.length; i++) m[i] = tmp[i];
        return m;
    };

    axlsign.sign = function (secretKey, msg, opt_random) {
        checkArrayTypes(secretKey, msg);
        if (secretKey.length !== 32) throw new Error('wrong secret key length');
        if (opt_random) {
            checkArrayTypes(opt_random);
            if (opt_random.length !== 64) throw new Error('wrong random data length');
        }
        var buf = new Uint8Array((opt_random ? 128 : 64) + msg.length);
        curve25519_sign(buf, msg, msg.length, secretKey, opt_random);
        var signature = new Uint8Array(64);
        for (var i = 0; i < signature.length; i++) signature[i] = buf[i];
        return signature;
    };

    axlsign.verify = function (publicKey, msg, signature) {
        checkArrayTypes(msg, signature, publicKey);
        if (signature.length !== 64) throw new Error('wrong signature length');
        if (publicKey.length !== 32) throw new Error('wrong public key length');
        var sm = new Uint8Array(64 + msg.length);
        var m = new Uint8Array(64 + msg.length);
        var i;
        for (i = 0; i < 64; i++) sm[i] = signature[i];
        for (i = 0; i < msg.length; i++) sm[i + 64] = msg[i];
        return (curve25519_sign_open(m, sm, sm.length, publicKey) >= 0);
    };

    axlsign.generateKeyPair = function (seed) {
        checkArrayTypes(seed);
        if (seed.length !== 32) throw new Error('wrong seed length');
        var sk = new Uint8Array(32);
        var pk = new Uint8Array(32);

        for (var i = 0; i < 32; i++) sk[i] = seed[i];

        crypto_scalarmult_base(pk, sk);

        // Turn secret key into the correct format.
        sk[0] &= 248;
        sk[31] &= 127;
        sk[31] |= 64;

        // Remove sign bit from public key.
        pk[31] &= 127;

        return {
            public: pk,
            private: sk
        };
    };

})(typeof module !== 'undefined' && module.exports ? module.exports : (self.axlsign = self.axlsign || {}));

/**
 * js-sha256 link https://github.com/emn178/js-sha256
 *
 * version 0.9.0
 * author Chen, Yi-Cyuan [emn178@gmail.com]
 * copyright Chen, Yi-Cyuan 2014-2017
 * license MIT
 */
/*jslint bitwise: true */
(function () {
    'use strict';

    var ERROR = 'input is invalid type';
    var WINDOW = typeof window === 'object';
    var root = WINDOW ? window : {};
    if (root.JS_SHA256_NO_WINDOW) {
        WINDOW = false;
    }
    var WEB_WORKER = !WINDOW && typeof self === 'object';
    var NODE_JS = !root.JS_SHA256_NO_NODE_JS && typeof process === 'object' && process.versions && process.versions.node;
    if (NODE_JS) {
        root = global;
    } else if (WEB_WORKER) {
        root = self;
    }
    var COMMON_JS = !root.JS_SHA256_NO_COMMON_JS && typeof module === 'object' && module.exports;
    var AMD = typeof define === 'function' && define.amd;
    var ARRAY_BUFFER = !root.JS_SHA256_NO_ARRAY_BUFFER && typeof ArrayBuffer !== 'undefined';
    var HEX_CHARS = '0123456789abcdef'.split('');
    var EXTRA = [-2147483648, 8388608, 32768, 128];
    var SHIFT = [24, 16, 8, 0];
    var K = [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
    ];
    var OUTPUT_TYPES = ['hex', 'array', 'digest', 'arrayBuffer'];

    var blocks = [];

    if (root.JS_SHA256_NO_NODE_JS || !Array.isArray) {
        Array.isArray = function (obj) {
            return Object.prototype.toString.call(obj) === '[object Array]';
        };
    }

    if (ARRAY_BUFFER && (root.JS_SHA256_NO_ARRAY_BUFFER_IS_VIEW || !ArrayBuffer.isView)) {
        ArrayBuffer.isView = function (obj) {
            return typeof obj === 'object' && obj.buffer && obj.buffer.constructor === ArrayBuffer;
        };
    }

    var createOutputMethod = function (outputType, is224) {
        return function (message) {
            return new Sha256(is224, true).update(message)[outputType]();
        };
    };

    var createMethod = function (is224) {
        var method = createOutputMethod('hex', is224);
        if (NODE_JS) {
            method = nodeWrap(method, is224);
        }
        method.create = function () {
            return new Sha256(is224);
        };
        method.update = function (message) {
            return method.create().update(message);
        };
        for (var i = 0; i < OUTPUT_TYPES.length; ++i) {
            var type = OUTPUT_TYPES[i];
            method[type] = createOutputMethod(type, is224);
        }
        return method;
    };

    var nodeWrap = function (method, is224) {
        var crypto = eval("require('crypto')");
        var Buffer = eval("require('buffer').Buffer");
        var algorithm = is224 ? 'sha224' : 'sha256';
        var nodeMethod = function (message) {
            if (typeof message === 'string') {
                return crypto.createHash(algorithm).update(message, 'utf8').digest('hex');
            } else {
                if (message === null || message === undefined) {
                    throw new Error(ERROR);
                } else if (message.constructor === ArrayBuffer) {
                    message = new Uint8Array(message);
                }
            }
            if (Array.isArray(message) || ArrayBuffer.isView(message) ||
                message.constructor === Buffer) {
                return crypto.createHash(algorithm).update(new Buffer(message)).digest('hex');
            } else {
                return method(message);
            }
        };
        return nodeMethod;
    };

    var createHmacOutputMethod = function (outputType, is224) {
        return function (key, message) {
            return new HmacSha256(key, is224, true).update(message)[outputType]();
        };
    };

    var createHmacMethod = function (is224) {
        var method = createHmacOutputMethod('hex', is224);
        method.create = function (key) {
            return new HmacSha256(key, is224);
        };
        method.update = function (key, message) {
            return method.create(key).update(message);
        };
        for (var i = 0; i < OUTPUT_TYPES.length; ++i) {
            var type = OUTPUT_TYPES[i];
            method[type] = createHmacOutputMethod(type, is224);
        }
        return method;
    };

    function Sha256(is224, sharedMemory) {
        if (sharedMemory) {
            blocks[0] = blocks[16] = blocks[1] = blocks[2] = blocks[3] =
                blocks[4] = blocks[5] = blocks[6] = blocks[7] =
                blocks[8] = blocks[9] = blocks[10] = blocks[11] =
                blocks[12] = blocks[13] = blocks[14] = blocks[15] = 0;
            this.blocks = blocks;
        } else {
            this.blocks = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        }

        if (is224) {
            this.h0 = 0xc1059ed8;
            this.h1 = 0x367cd507;
            this.h2 = 0x3070dd17;
            this.h3 = 0xf70e5939;
            this.h4 = 0xffc00b31;
            this.h5 = 0x68581511;
            this.h6 = 0x64f98fa7;
            this.h7 = 0xbefa4fa4;
        } else { // 256
            this.h0 = 0x6a09e667;
            this.h1 = 0xbb67ae85;
            this.h2 = 0x3c6ef372;
            this.h3 = 0xa54ff53a;
            this.h4 = 0x510e527f;
            this.h5 = 0x9b05688c;
            this.h6 = 0x1f83d9ab;
            this.h7 = 0x5be0cd19;
        }

        this.block = this.start = this.bytes = this.hBytes = 0;
        this.finalized = this.hashed = false;
        this.first = true;
        this.is224 = is224;
    }

    Sha256.prototype.update = function (message) {
        if (this.finalized) {
            return;
        }
        var notString, type = typeof message;
        if (type !== 'string') {
            if (type === 'object') {
                if (message === null) {
                    throw new Error(ERROR);
                } else if (ARRAY_BUFFER && message.constructor === ArrayBuffer) {
                    message = new Uint8Array(message);
                } else if (!Array.isArray(message)) {
                    if (!ARRAY_BUFFER || !ArrayBuffer.isView(message)) {
                        throw new Error(ERROR);
                    }
                }
            } else {
                throw new Error(ERROR);
            }
            notString = true;
        }
        var code, index = 0, i, length = message.length, blocks = this.blocks;

        while (index < length) {
            if (this.hashed) {
                this.hashed = false;
                blocks[0] = this.block;
                blocks[16] = blocks[1] = blocks[2] = blocks[3] =
                    blocks[4] = blocks[5] = blocks[6] = blocks[7] =
                    blocks[8] = blocks[9] = blocks[10] = blocks[11] =
                    blocks[12] = blocks[13] = blocks[14] = blocks[15] = 0;
            }

            if (notString) {
                for (i = this.start; index < length && i < 64; ++index) {
                    blocks[i >> 2] |= message[index] << SHIFT[i++ & 3];
                }
            } else {
                for (i = this.start; index < length && i < 64; ++index) {
                    code = message.charCodeAt(index);
                    if (code < 0x80) {
                        blocks[i >> 2] |= code << SHIFT[i++ & 3];
                    } else if (code < 0x800) {
                        blocks[i >> 2] |= (0xc0 | (code >> 6)) << SHIFT[i++ & 3];
                        blocks[i >> 2] |= (0x80 | (code & 0x3f)) << SHIFT[i++ & 3];
                    } else if (code < 0xd800 || code >= 0xe000) {
                        blocks[i >> 2] |= (0xe0 | (code >> 12)) << SHIFT[i++ & 3];
                        blocks[i >> 2] |= (0x80 | ((code >> 6) & 0x3f)) << SHIFT[i++ & 3];
                        blocks[i >> 2] |= (0x80 | (code & 0x3f)) << SHIFT[i++ & 3];
                    } else {
                        code = 0x10000 + (((code & 0x3ff) << 10) | (message.charCodeAt(++index) & 0x3ff));
                        blocks[i >> 2] |= (0xf0 | (code >> 18)) << SHIFT[i++ & 3];
                        blocks[i >> 2] |= (0x80 | ((code >> 12) & 0x3f)) << SHIFT[i++ & 3];
                        blocks[i >> 2] |= (0x80 | ((code >> 6) & 0x3f)) << SHIFT[i++ & 3];
                        blocks[i >> 2] |= (0x80 | (code & 0x3f)) << SHIFT[i++ & 3];
                    }
                }
            }

            this.lastByteIndex = i;
            this.bytes += i - this.start;
            if (i >= 64) {
                this.block = blocks[16];
                this.start = i - 64;
                this.hash();
                this.hashed = true;
            } else {
                this.start = i;
            }
        }
        if (this.bytes > 4294967295) {
            this.hBytes += this.bytes / 4294967296 << 0;
            this.bytes = this.bytes % 4294967296;
        }
        return this;
    };

    Sha256.prototype.finalize = function () {
        if (this.finalized) {
            return;
        }
        this.finalized = true;
        var blocks = this.blocks, i = this.lastByteIndex;
        blocks[16] = this.block;
        blocks[i >> 2] |= EXTRA[i & 3];
        this.block = blocks[16];
        if (i >= 56) {
            if (!this.hashed) {
                this.hash();
            }
            blocks[0] = this.block;
            blocks[16] = blocks[1] = blocks[2] = blocks[3] =
                blocks[4] = blocks[5] = blocks[6] = blocks[7] =
                blocks[8] = blocks[9] = blocks[10] = blocks[11] =
                blocks[12] = blocks[13] = blocks[14] = blocks[15] = 0;
        }
        blocks[14] = this.hBytes << 3 | this.bytes >>> 29;
        blocks[15] = this.bytes << 3;
        this.hash();
    };

    Sha256.prototype.hash = function () {
        var a = this.h0, b = this.h1, c = this.h2, d = this.h3, e = this.h4, f = this.h5, g = this.h6,
            h = this.h7, blocks = this.blocks, j, s0, s1, maj, t1, t2, ch, ab, da, cd, bc;

        for (j = 16; j < 64; ++j) {
            // rightrotate
            t1 = blocks[j - 15];
            s0 = ((t1 >>> 7) | (t1 << 25)) ^ ((t1 >>> 18) | (t1 << 14)) ^ (t1 >>> 3);
            t1 = blocks[j - 2];
            s1 = ((t1 >>> 17) | (t1 << 15)) ^ ((t1 >>> 19) | (t1 << 13)) ^ (t1 >>> 10);
            blocks[j] = blocks[j - 16] + s0 + blocks[j - 7] + s1 << 0;
        }

        bc = b & c;
        for (j = 0; j < 64; j += 4) {
            if (this.first) {
                if (this.is224) {
                    ab = 300032;
                    t1 = blocks[0] - 1413257819;
                    h = t1 - 150054599 << 0;
                    d = t1 + 24177077 << 0;
                } else {
                    ab = 704751109;
                    t1 = blocks[0] - 210244248;
                    h = t1 - 1521486534 << 0;
                    d = t1 + 143694565 << 0;
                }
                this.first = false;
            } else {
                s0 = ((a >>> 2) | (a << 30)) ^ ((a >>> 13) | (a << 19)) ^ ((a >>> 22) | (a << 10));
                s1 = ((e >>> 6) | (e << 26)) ^ ((e >>> 11) | (e << 21)) ^ ((e >>> 25) | (e << 7));
                ab = a & b;
                maj = ab ^ (a & c) ^ bc;
                ch = (e & f) ^ (~e & g);
                t1 = h + s1 + ch + K[j] + blocks[j];
                t2 = s0 + maj;
                h = d + t1 << 0;
                d = t1 + t2 << 0;
            }
            s0 = ((d >>> 2) | (d << 30)) ^ ((d >>> 13) | (d << 19)) ^ ((d >>> 22) | (d << 10));
            s1 = ((h >>> 6) | (h << 26)) ^ ((h >>> 11) | (h << 21)) ^ ((h >>> 25) | (h << 7));
            da = d & a;
            maj = da ^ (d & b) ^ ab;
            ch = (h & e) ^ (~h & f);
            t1 = g + s1 + ch + K[j + 1] + blocks[j + 1];
            t2 = s0 + maj;
            g = c + t1 << 0;
            c = t1 + t2 << 0;
            s0 = ((c >>> 2) | (c << 30)) ^ ((c >>> 13) | (c << 19)) ^ ((c >>> 22) | (c << 10));
            s1 = ((g >>> 6) | (g << 26)) ^ ((g >>> 11) | (g << 21)) ^ ((g >>> 25) | (g << 7));
            cd = c & d;
            maj = cd ^ (c & a) ^ da;
            ch = (g & h) ^ (~g & e);
            t1 = f + s1 + ch + K[j + 2] + blocks[j + 2];
            t2 = s0 + maj;
            f = b + t1 << 0;
            b = t1 + t2 << 0;
            s0 = ((b >>> 2) | (b << 30)) ^ ((b >>> 13) | (b << 19)) ^ ((b >>> 22) | (b << 10));
            s1 = ((f >>> 6) | (f << 26)) ^ ((f >>> 11) | (f << 21)) ^ ((f >>> 25) | (f << 7));
            bc = b & c;
            maj = bc ^ (b & d) ^ cd;
            ch = (f & g) ^ (~f & h);
            t1 = e + s1 + ch + K[j + 3] + blocks[j + 3];
            t2 = s0 + maj;
            e = a + t1 << 0;
            a = t1 + t2 << 0;
        }

        this.h0 = this.h0 + a << 0;
        this.h1 = this.h1 + b << 0;
        this.h2 = this.h2 + c << 0;
        this.h3 = this.h3 + d << 0;
        this.h4 = this.h4 + e << 0;
        this.h5 = this.h5 + f << 0;
        this.h6 = this.h6 + g << 0;
        this.h7 = this.h7 + h << 0;
    };

    Sha256.prototype.hex = function () {
        this.finalize();

        var h0 = this.h0, h1 = this.h1, h2 = this.h2, h3 = this.h3, h4 = this.h4, h5 = this.h5,
            h6 = this.h6, h7 = this.h7;

        var hex = HEX_CHARS[(h0 >> 28) & 0x0F] + HEX_CHARS[(h0 >> 24) & 0x0F] +
            HEX_CHARS[(h0 >> 20) & 0x0F] + HEX_CHARS[(h0 >> 16) & 0x0F] +
            HEX_CHARS[(h0 >> 12) & 0x0F] + HEX_CHARS[(h0 >> 8) & 0x0F] +
            HEX_CHARS[(h0 >> 4) & 0x0F] + HEX_CHARS[h0 & 0x0F] +
            HEX_CHARS[(h1 >> 28) & 0x0F] + HEX_CHARS[(h1 >> 24) & 0x0F] +
            HEX_CHARS[(h1 >> 20) & 0x0F] + HEX_CHARS[(h1 >> 16) & 0x0F] +
            HEX_CHARS[(h1 >> 12) & 0x0F] + HEX_CHARS[(h1 >> 8) & 0x0F] +
            HEX_CHARS[(h1 >> 4) & 0x0F] + HEX_CHARS[h1 & 0x0F] +
            HEX_CHARS[(h2 >> 28) & 0x0F] + HEX_CHARS[(h2 >> 24) & 0x0F] +
            HEX_CHARS[(h2 >> 20) & 0x0F] + HEX_CHARS[(h2 >> 16) & 0x0F] +
            HEX_CHARS[(h2 >> 12) & 0x0F] + HEX_CHARS[(h2 >> 8) & 0x0F] +
            HEX_CHARS[(h2 >> 4) & 0x0F] + HEX_CHARS[h2 & 0x0F] +
            HEX_CHARS[(h3 >> 28) & 0x0F] + HEX_CHARS[(h3 >> 24) & 0x0F] +
            HEX_CHARS[(h3 >> 20) & 0x0F] + HEX_CHARS[(h3 >> 16) & 0x0F] +
            HEX_CHARS[(h3 >> 12) & 0x0F] + HEX_CHARS[(h3 >> 8) & 0x0F] +
            HEX_CHARS[(h3 >> 4) & 0x0F] + HEX_CHARS[h3 & 0x0F] +
            HEX_CHARS[(h4 >> 28) & 0x0F] + HEX_CHARS[(h4 >> 24) & 0x0F] +
            HEX_CHARS[(h4 >> 20) & 0x0F] + HEX_CHARS[(h4 >> 16) & 0x0F] +
            HEX_CHARS[(h4 >> 12) & 0x0F] + HEX_CHARS[(h4 >> 8) & 0x0F] +
            HEX_CHARS[(h4 >> 4) & 0x0F] + HEX_CHARS[h4 & 0x0F] +
            HEX_CHARS[(h5 >> 28) & 0x0F] + HEX_CHARS[(h5 >> 24) & 0x0F] +
            HEX_CHARS[(h5 >> 20) & 0x0F] + HEX_CHARS[(h5 >> 16) & 0x0F] +
            HEX_CHARS[(h5 >> 12) & 0x0F] + HEX_CHARS[(h5 >> 8) & 0x0F] +
            HEX_CHARS[(h5 >> 4) & 0x0F] + HEX_CHARS[h5 & 0x0F] +
            HEX_CHARS[(h6 >> 28) & 0x0F] + HEX_CHARS[(h6 >> 24) & 0x0F] +
            HEX_CHARS[(h6 >> 20) & 0x0F] + HEX_CHARS[(h6 >> 16) & 0x0F] +
            HEX_CHARS[(h6 >> 12) & 0x0F] + HEX_CHARS[(h6 >> 8) & 0x0F] +
            HEX_CHARS[(h6 >> 4) & 0x0F] + HEX_CHARS[h6 & 0x0F];
        if (!this.is224) {
            hex += HEX_CHARS[(h7 >> 28) & 0x0F] + HEX_CHARS[(h7 >> 24) & 0x0F] +
                HEX_CHARS[(h7 >> 20) & 0x0F] + HEX_CHARS[(h7 >> 16) & 0x0F] +
                HEX_CHARS[(h7 >> 12) & 0x0F] + HEX_CHARS[(h7 >> 8) & 0x0F] +
                HEX_CHARS[(h7 >> 4) & 0x0F] + HEX_CHARS[h7 & 0x0F];
        }
        return hex;
    };

    Sha256.prototype.toString = Sha256.prototype.hex;

    Sha256.prototype.digest = function () {
        this.finalize();

        var h0 = this.h0, h1 = this.h1, h2 = this.h2, h3 = this.h3, h4 = this.h4, h5 = this.h5,
            h6 = this.h6, h7 = this.h7;

        var arr = [
            (h0 >> 24) & 0xFF, (h0 >> 16) & 0xFF, (h0 >> 8) & 0xFF, h0 & 0xFF,
            (h1 >> 24) & 0xFF, (h1 >> 16) & 0xFF, (h1 >> 8) & 0xFF, h1 & 0xFF,
            (h2 >> 24) & 0xFF, (h2 >> 16) & 0xFF, (h2 >> 8) & 0xFF, h2 & 0xFF,
            (h3 >> 24) & 0xFF, (h3 >> 16) & 0xFF, (h3 >> 8) & 0xFF, h3 & 0xFF,
            (h4 >> 24) & 0xFF, (h4 >> 16) & 0xFF, (h4 >> 8) & 0xFF, h4 & 0xFF,
            (h5 >> 24) & 0xFF, (h5 >> 16) & 0xFF, (h5 >> 8) & 0xFF, h5 & 0xFF,
            (h6 >> 24) & 0xFF, (h6 >> 16) & 0xFF, (h6 >> 8) & 0xFF, h6 & 0xFF
        ];
        if (!this.is224) {
            arr.push((h7 >> 24) & 0xFF, (h7 >> 16) & 0xFF, (h7 >> 8) & 0xFF, h7 & 0xFF);
        }
        return arr;
    };

    Sha256.prototype.array = Sha256.prototype.digest;

    Sha256.prototype.arrayBuffer = function () {
        this.finalize();

        var buffer = new ArrayBuffer(this.is224 ? 28 : 32);
        var dataView = new DataView(buffer);
        dataView.setUint32(0, this.h0);
        dataView.setUint32(4, this.h1);
        dataView.setUint32(8, this.h2);
        dataView.setUint32(12, this.h3);
        dataView.setUint32(16, this.h4);
        dataView.setUint32(20, this.h5);
        dataView.setUint32(24, this.h6);
        if (!this.is224) {
            dataView.setUint32(28, this.h7);
        }
        return buffer;
    };

    function HmacSha256(key, is224, sharedMemory) {
        var i, type = typeof key;
        if (type === 'string') {
            var bytes = [], length = key.length, index = 0, code;
            for (i = 0; i < length; ++i) {
                code = key.charCodeAt(i);
                if (code < 0x80) {
                    bytes[index++] = code;
                } else if (code < 0x800) {
                    bytes[index++] = (0xc0 | (code >> 6));
                    bytes[index++] = (0x80 | (code & 0x3f));
                } else if (code < 0xd800 || code >= 0xe000) {
                    bytes[index++] = (0xe0 | (code >> 12));
                    bytes[index++] = (0x80 | ((code >> 6) & 0x3f));
                    bytes[index++] = (0x80 | (code & 0x3f));
                } else {
                    code = 0x10000 + (((code & 0x3ff) << 10) | (key.charCodeAt(++i) & 0x3ff));
                    bytes[index++] = (0xf0 | (code >> 18));
                    bytes[index++] = (0x80 | ((code >> 12) & 0x3f));
                    bytes[index++] = (0x80 | ((code >> 6) & 0x3f));
                    bytes[index++] = (0x80 | (code & 0x3f));
                }
            }
            key = bytes;
        } else {
            if (type === 'object') {
                if (key === null) {
                    throw new Error(ERROR);
                } else if (ARRAY_BUFFER && key.constructor === ArrayBuffer) {
                    key = new Uint8Array(key);
                } else if (!Array.isArray(key)) {
                    if (!ARRAY_BUFFER || !ArrayBuffer.isView(key)) {
                        throw new Error(ERROR);
                    }
                }
            } else {
                throw new Error(ERROR);
            }
        }

        if (key.length > 64) {
            key = (new Sha256(is224, true)).update(key).array();
        }

        var oKeyPad = [], iKeyPad = [];
        for (i = 0; i < 64; ++i) {
            var b = key[i] || 0;
            oKeyPad[i] = 0x5c ^ b;
            iKeyPad[i] = 0x36 ^ b;
        }

        Sha256.call(this, is224, sharedMemory);

        this.update(iKeyPad);
        this.oKeyPad = oKeyPad;
        this.inner = true;
        this.sharedMemory = sharedMemory;
    }
    HmacSha256.prototype = new Sha256();

    HmacSha256.prototype.finalize = function () {
        Sha256.prototype.finalize.call(this);
        if (this.inner) {
            this.inner = false;
            var innerHash = this.array();
            Sha256.call(this, this.is224, this.sharedMemory);
            this.update(this.oKeyPad);
            this.update(innerHash);
            Sha256.prototype.finalize.call(this);
        }
    };

    var exports = createMethod();
    exports.sha256 = exports;
    exports.sha224 = createMethod(true);
    exports.sha256.hmac = createHmacMethod();
    exports.sha224.hmac = createHmacMethod(true);

    if (COMMON_JS) {
        module.exports = exports;
    } else {
        root.sha256 = exports.sha256;
        root.sha224 = exports.sha224;
        if (AMD) {
            define(function () {
                return exports;
            });
        }
    }
})();

/*! MIT License. Copyright 2015-2018 Richard Moore <me@ricmoo.com>. See LICENSE.txt.
 https://github.com/ricmoo/aes-js
 */
(function (root) {
    "use strict";

    function checkInt(value) {
        return (parseInt(value) === value);
    }

    function checkInts(arrayish) {
        if (!checkInt(arrayish.length)) { return false; }

        for (var i = 0; i < arrayish.length; i++) {
            if (!checkInt(arrayish[i]) || arrayish[i] < 0 || arrayish[i] > 255) {
                return false;
            }
        }

        return true;
    }

    function coerceArray(arg, copy) {

        // ArrayBuffer view
        if (arg.buffer && arg.name === 'Uint8Array') {

            if (copy) {
                if (arg.slice) {
                    arg = arg.slice();
                } else {
                    arg = Array.prototype.slice.call(arg);
                }
            }

            return arg;
        }

        // It's an array; check it is a valid representation of a byte
        if (Array.isArray(arg)) {
            if (!checkInts(arg)) {
                throw new Error('Array contains invalid value: ' + arg);
            }

            return new Uint8Array(arg);
        }

        // Something else, but behaves like an array (maybe a Buffer? Arguments?)
        if (checkInt(arg.length) && checkInts(arg)) {
            return new Uint8Array(arg);
        }

        throw new Error('unsupported array-like object');
    }

    function createArray(length) {
        return new Uint8Array(length);
    }

    function copyArray(sourceArray, targetArray, targetStart, sourceStart, sourceEnd) {
        if (sourceStart != null || sourceEnd != null) {
            if (sourceArray.slice) {
                sourceArray = sourceArray.slice(sourceStart, sourceEnd);
            } else {
                sourceArray = Array.prototype.slice.call(sourceArray, sourceStart, sourceEnd);
            }
        }
        targetArray.set(sourceArray, targetStart);
    }



    var convertUtf8 = (function () {
        function toBytes(text) {
            var result = [], i = 0;
            text = encodeURI(text);
            while (i < text.length) {
                var c = text.charCodeAt(i++);

                // if it is a % sign, encode the following 2 bytes as a hex value
                if (c === 37) {
                    result.push(parseInt(text.substr(i, 2), 16))
                    i += 2;

                    // otherwise, just the actual byte
                } else {
                    result.push(c)
                }
            }

            return coerceArray(result);
        }

        function fromBytes(bytes) {
            var result = [], i = 0;

            while (i < bytes.length) {
                var c = bytes[i];

                if (c < 128) {
                    result.push(String.fromCharCode(c));
                    i++;
                } else if (c > 191 && c < 224) {
                    result.push(String.fromCharCode(((c & 0x1f) << 6) | (bytes[i + 1] & 0x3f)));
                    i += 2;
                } else {
                    result.push(String.fromCharCode(((c & 0x0f) << 12) | ((bytes[i + 1] & 0x3f) << 6) | (bytes[i + 2] & 0x3f)));
                    i += 3;
                }
            }

            return result.join('');
        }

        return {
            toBytes: toBytes,
            fromBytes: fromBytes,
        }
    })();

    var convertHex = (function () {
        function toBytes(text) {
            var result = [];
            for (var i = 0; i < text.length; i += 2) {
                result.push(parseInt(text.substr(i, 2), 16));
            }

            return result;
        }

        // http://ixti.net/development/javascript/2011/11/11/base64-encodedecode-of-utf8-in-browser-with-js.html
        var Hex = '0123456789abcdef';

        function fromBytes(bytes) {
            var result = [];
            for (var i = 0; i < bytes.length; i++) {
                var v = bytes[i];
                result.push(Hex[(v & 0xf0) >> 4] + Hex[v & 0x0f]);
            }
            return result.join('');
        }

        return {
            toBytes: toBytes,
            fromBytes: fromBytes,
        }
    })();


    // Number of rounds by keysize
    var numberOfRounds = { 16: 10, 24: 12, 32: 14 }

    // Round constant words
    var rcon = [0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1b, 0x36, 0x6c, 0xd8, 0xab, 0x4d, 0x9a, 0x2f, 0x5e, 0xbc, 0x63, 0xc6, 0x97, 0x35, 0x6a, 0xd4, 0xb3, 0x7d, 0xfa, 0xef, 0xc5, 0x91];

    // S-box and Inverse S-box (S is for Substitution)
    var S = [0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76, 0xca, 0x82, 0xc9, 0x7d, 0xfa, 0x59, 0x47, 0xf0, 0xad, 0xd4, 0xa2, 0xaf, 0x9c, 0xa4, 0x72, 0xc0, 0xb7, 0xfd, 0x93, 0x26, 0x36, 0x3f, 0xf7, 0xcc, 0x34, 0xa5, 0xe5, 0xf1, 0x71, 0xd8, 0x31, 0x15, 0x04, 0xc7, 0x23, 0xc3, 0x18, 0x96, 0x05, 0x9a, 0x07, 0x12, 0x80, 0xe2, 0xeb, 0x27, 0xb2, 0x75, 0x09, 0x83, 0x2c, 0x1a, 0x1b, 0x6e, 0x5a, 0xa0, 0x52, 0x3b, 0xd6, 0xb3, 0x29, 0xe3, 0x2f, 0x84, 0x53, 0xd1, 0x00, 0xed, 0x20, 0xfc, 0xb1, 0x5b, 0x6a, 0xcb, 0xbe, 0x39, 0x4a, 0x4c, 0x58, 0xcf, 0xd0, 0xef, 0xaa, 0xfb, 0x43, 0x4d, 0x33, 0x85, 0x45, 0xf9, 0x02, 0x7f, 0x50, 0x3c, 0x9f, 0xa8, 0x51, 0xa3, 0x40, 0x8f, 0x92, 0x9d, 0x38, 0xf5, 0xbc, 0xb6, 0xda, 0x21, 0x10, 0xff, 0xf3, 0xd2, 0xcd, 0x0c, 0x13, 0xec, 0x5f, 0x97, 0x44, 0x17, 0xc4, 0xa7, 0x7e, 0x3d, 0x64, 0x5d, 0x19, 0x73, 0x60, 0x81, 0x4f, 0xdc, 0x22, 0x2a, 0x90, 0x88, 0x46, 0xee, 0xb8, 0x14, 0xde, 0x5e, 0x0b, 0xdb, 0xe0, 0x32, 0x3a, 0x0a, 0x49, 0x06, 0x24, 0x5c, 0xc2, 0xd3, 0xac, 0x62, 0x91, 0x95, 0xe4, 0x79, 0xe7, 0xc8, 0x37, 0x6d, 0x8d, 0xd5, 0x4e, 0xa9, 0x6c, 0x56, 0xf4, 0xea, 0x65, 0x7a, 0xae, 0x08, 0xba, 0x78, 0x25, 0x2e, 0x1c, 0xa6, 0xb4, 0xc6, 0xe8, 0xdd, 0x74, 0x1f, 0x4b, 0xbd, 0x8b, 0x8a, 0x70, 0x3e, 0xb5, 0x66, 0x48, 0x03, 0xf6, 0x0e, 0x61, 0x35, 0x57, 0xb9, 0x86, 0xc1, 0x1d, 0x9e, 0xe1, 0xf8, 0x98, 0x11, 0x69, 0xd9, 0x8e, 0x94, 0x9b, 0x1e, 0x87, 0xe9, 0xce, 0x55, 0x28, 0xdf, 0x8c, 0xa1, 0x89, 0x0d, 0xbf, 0xe6, 0x42, 0x68, 0x41, 0x99, 0x2d, 0x0f, 0xb0, 0x54, 0xbb, 0x16];
    var Si = [0x52, 0x09, 0x6a, 0xd5, 0x30, 0x36, 0xa5, 0x38, 0xbf, 0x40, 0xa3, 0x9e, 0x81, 0xf3, 0xd7, 0xfb, 0x7c, 0xe3, 0x39, 0x82, 0x9b, 0x2f, 0xff, 0x87, 0x34, 0x8e, 0x43, 0x44, 0xc4, 0xde, 0xe9, 0xcb, 0x54, 0x7b, 0x94, 0x32, 0xa6, 0xc2, 0x23, 0x3d, 0xee, 0x4c, 0x95, 0x0b, 0x42, 0xfa, 0xc3, 0x4e, 0x08, 0x2e, 0xa1, 0x66, 0x28, 0xd9, 0x24, 0xb2, 0x76, 0x5b, 0xa2, 0x49, 0x6d, 0x8b, 0xd1, 0x25, 0x72, 0xf8, 0xf6, 0x64, 0x86, 0x68, 0x98, 0x16, 0xd4, 0xa4, 0x5c, 0xcc, 0x5d, 0x65, 0xb6, 0x92, 0x6c, 0x70, 0x48, 0x50, 0xfd, 0xed, 0xb9, 0xda, 0x5e, 0x15, 0x46, 0x57, 0xa7, 0x8d, 0x9d, 0x84, 0x90, 0xd8, 0xab, 0x00, 0x8c, 0xbc, 0xd3, 0x0a, 0xf7, 0xe4, 0x58, 0x05, 0xb8, 0xb3, 0x45, 0x06, 0xd0, 0x2c, 0x1e, 0x8f, 0xca, 0x3f, 0x0f, 0x02, 0xc1, 0xaf, 0xbd, 0x03, 0x01, 0x13, 0x8a, 0x6b, 0x3a, 0x91, 0x11, 0x41, 0x4f, 0x67, 0xdc, 0xea, 0x97, 0xf2, 0xcf, 0xce, 0xf0, 0xb4, 0xe6, 0x73, 0x96, 0xac, 0x74, 0x22, 0xe7, 0xad, 0x35, 0x85, 0xe2, 0xf9, 0x37, 0xe8, 0x1c, 0x75, 0xdf, 0x6e, 0x47, 0xf1, 0x1a, 0x71, 0x1d, 0x29, 0xc5, 0x89, 0x6f, 0xb7, 0x62, 0x0e, 0xaa, 0x18, 0xbe, 0x1b, 0xfc, 0x56, 0x3e, 0x4b, 0xc6, 0xd2, 0x79, 0x20, 0x9a, 0xdb, 0xc0, 0xfe, 0x78, 0xcd, 0x5a, 0xf4, 0x1f, 0xdd, 0xa8, 0x33, 0x88, 0x07, 0xc7, 0x31, 0xb1, 0x12, 0x10, 0x59, 0x27, 0x80, 0xec, 0x5f, 0x60, 0x51, 0x7f, 0xa9, 0x19, 0xb5, 0x4a, 0x0d, 0x2d, 0xe5, 0x7a, 0x9f, 0x93, 0xc9, 0x9c, 0xef, 0xa0, 0xe0, 0x3b, 0x4d, 0xae, 0x2a, 0xf5, 0xb0, 0xc8, 0xeb, 0xbb, 0x3c, 0x83, 0x53, 0x99, 0x61, 0x17, 0x2b, 0x04, 0x7e, 0xba, 0x77, 0xd6, 0x26, 0xe1, 0x69, 0x14, 0x63, 0x55, 0x21, 0x0c, 0x7d];

    // Transformations for encryption
    var T1 = [0xc66363a5, 0xf87c7c84, 0xee777799, 0xf67b7b8d, 0xfff2f20d, 0xd66b6bbd, 0xde6f6fb1, 0x91c5c554, 0x60303050, 0x02010103, 0xce6767a9, 0x562b2b7d, 0xe7fefe19, 0xb5d7d762, 0x4dababe6, 0xec76769a, 0x8fcaca45, 0x1f82829d, 0x89c9c940, 0xfa7d7d87, 0xeffafa15, 0xb25959eb, 0x8e4747c9, 0xfbf0f00b, 0x41adadec, 0xb3d4d467, 0x5fa2a2fd, 0x45afafea, 0x239c9cbf, 0x53a4a4f7, 0xe4727296, 0x9bc0c05b, 0x75b7b7c2, 0xe1fdfd1c, 0x3d9393ae, 0x4c26266a, 0x6c36365a, 0x7e3f3f41, 0xf5f7f702, 0x83cccc4f, 0x6834345c, 0x51a5a5f4, 0xd1e5e534, 0xf9f1f108, 0xe2717193, 0xabd8d873, 0x62313153, 0x2a15153f, 0x0804040c, 0x95c7c752, 0x46232365, 0x9dc3c35e, 0x30181828, 0x379696a1, 0x0a05050f, 0x2f9a9ab5, 0x0e070709, 0x24121236, 0x1b80809b, 0xdfe2e23d, 0xcdebeb26, 0x4e272769, 0x7fb2b2cd, 0xea75759f, 0x1209091b, 0x1d83839e, 0x582c2c74, 0x341a1a2e, 0x361b1b2d, 0xdc6e6eb2, 0xb45a5aee, 0x5ba0a0fb, 0xa45252f6, 0x763b3b4d, 0xb7d6d661, 0x7db3b3ce, 0x5229297b, 0xdde3e33e, 0x5e2f2f71, 0x13848497, 0xa65353f5, 0xb9d1d168, 0x00000000, 0xc1eded2c, 0x40202060, 0xe3fcfc1f, 0x79b1b1c8, 0xb65b5bed, 0xd46a6abe, 0x8dcbcb46, 0x67bebed9, 0x7239394b, 0x944a4ade, 0x984c4cd4, 0xb05858e8, 0x85cfcf4a, 0xbbd0d06b, 0xc5efef2a, 0x4faaaae5, 0xedfbfb16, 0x864343c5, 0x9a4d4dd7, 0x66333355, 0x11858594, 0x8a4545cf, 0xe9f9f910, 0x04020206, 0xfe7f7f81, 0xa05050f0, 0x783c3c44, 0x259f9fba, 0x4ba8a8e3, 0xa25151f3, 0x5da3a3fe, 0x804040c0, 0x058f8f8a, 0x3f9292ad, 0x219d9dbc, 0x70383848, 0xf1f5f504, 0x63bcbcdf, 0x77b6b6c1, 0xafdada75, 0x42212163, 0x20101030, 0xe5ffff1a, 0xfdf3f30e, 0xbfd2d26d, 0x81cdcd4c, 0x180c0c14, 0x26131335, 0xc3ecec2f, 0xbe5f5fe1, 0x359797a2, 0x884444cc, 0x2e171739, 0x93c4c457, 0x55a7a7f2, 0xfc7e7e82, 0x7a3d3d47, 0xc86464ac, 0xba5d5de7, 0x3219192b, 0xe6737395, 0xc06060a0, 0x19818198, 0x9e4f4fd1, 0xa3dcdc7f, 0x44222266, 0x542a2a7e, 0x3b9090ab, 0x0b888883, 0x8c4646ca, 0xc7eeee29, 0x6bb8b8d3, 0x2814143c, 0xa7dede79, 0xbc5e5ee2, 0x160b0b1d, 0xaddbdb76, 0xdbe0e03b, 0x64323256, 0x743a3a4e, 0x140a0a1e, 0x924949db, 0x0c06060a, 0x4824246c, 0xb85c5ce4, 0x9fc2c25d, 0xbdd3d36e, 0x43acacef, 0xc46262a6, 0x399191a8, 0x319595a4, 0xd3e4e437, 0xf279798b, 0xd5e7e732, 0x8bc8c843, 0x6e373759, 0xda6d6db7, 0x018d8d8c, 0xb1d5d564, 0x9c4e4ed2, 0x49a9a9e0, 0xd86c6cb4, 0xac5656fa, 0xf3f4f407, 0xcfeaea25, 0xca6565af, 0xf47a7a8e, 0x47aeaee9, 0x10080818, 0x6fbabad5, 0xf0787888, 0x4a25256f, 0x5c2e2e72, 0x381c1c24, 0x57a6a6f1, 0x73b4b4c7, 0x97c6c651, 0xcbe8e823, 0xa1dddd7c, 0xe874749c, 0x3e1f1f21, 0x964b4bdd, 0x61bdbddc, 0x0d8b8b86, 0x0f8a8a85, 0xe0707090, 0x7c3e3e42, 0x71b5b5c4, 0xcc6666aa, 0x904848d8, 0x06030305, 0xf7f6f601, 0x1c0e0e12, 0xc26161a3, 0x6a35355f, 0xae5757f9, 0x69b9b9d0, 0x17868691, 0x99c1c158, 0x3a1d1d27, 0x279e9eb9, 0xd9e1e138, 0xebf8f813, 0x2b9898b3, 0x22111133, 0xd26969bb, 0xa9d9d970, 0x078e8e89, 0x339494a7, 0x2d9b9bb6, 0x3c1e1e22, 0x15878792, 0xc9e9e920, 0x87cece49, 0xaa5555ff, 0x50282878, 0xa5dfdf7a, 0x038c8c8f, 0x59a1a1f8, 0x09898980, 0x1a0d0d17, 0x65bfbfda, 0xd7e6e631, 0x844242c6, 0xd06868b8, 0x824141c3, 0x299999b0, 0x5a2d2d77, 0x1e0f0f11, 0x7bb0b0cb, 0xa85454fc, 0x6dbbbbd6, 0x2c16163a];
    var T2 = [0xa5c66363, 0x84f87c7c, 0x99ee7777, 0x8df67b7b, 0x0dfff2f2, 0xbdd66b6b, 0xb1de6f6f, 0x5491c5c5, 0x50603030, 0x03020101, 0xa9ce6767, 0x7d562b2b, 0x19e7fefe, 0x62b5d7d7, 0xe64dabab, 0x9aec7676, 0x458fcaca, 0x9d1f8282, 0x4089c9c9, 0x87fa7d7d, 0x15effafa, 0xebb25959, 0xc98e4747, 0x0bfbf0f0, 0xec41adad, 0x67b3d4d4, 0xfd5fa2a2, 0xea45afaf, 0xbf239c9c, 0xf753a4a4, 0x96e47272, 0x5b9bc0c0, 0xc275b7b7, 0x1ce1fdfd, 0xae3d9393, 0x6a4c2626, 0x5a6c3636, 0x417e3f3f, 0x02f5f7f7, 0x4f83cccc, 0x5c683434, 0xf451a5a5, 0x34d1e5e5, 0x08f9f1f1, 0x93e27171, 0x73abd8d8, 0x53623131, 0x3f2a1515, 0x0c080404, 0x5295c7c7, 0x65462323, 0x5e9dc3c3, 0x28301818, 0xa1379696, 0x0f0a0505, 0xb52f9a9a, 0x090e0707, 0x36241212, 0x9b1b8080, 0x3ddfe2e2, 0x26cdebeb, 0x694e2727, 0xcd7fb2b2, 0x9fea7575, 0x1b120909, 0x9e1d8383, 0x74582c2c, 0x2e341a1a, 0x2d361b1b, 0xb2dc6e6e, 0xeeb45a5a, 0xfb5ba0a0, 0xf6a45252, 0x4d763b3b, 0x61b7d6d6, 0xce7db3b3, 0x7b522929, 0x3edde3e3, 0x715e2f2f, 0x97138484, 0xf5a65353, 0x68b9d1d1, 0x00000000, 0x2cc1eded, 0x60402020, 0x1fe3fcfc, 0xc879b1b1, 0xedb65b5b, 0xbed46a6a, 0x468dcbcb, 0xd967bebe, 0x4b723939, 0xde944a4a, 0xd4984c4c, 0xe8b05858, 0x4a85cfcf, 0x6bbbd0d0, 0x2ac5efef, 0xe54faaaa, 0x16edfbfb, 0xc5864343, 0xd79a4d4d, 0x55663333, 0x94118585, 0xcf8a4545, 0x10e9f9f9, 0x06040202, 0x81fe7f7f, 0xf0a05050, 0x44783c3c, 0xba259f9f, 0xe34ba8a8, 0xf3a25151, 0xfe5da3a3, 0xc0804040, 0x8a058f8f, 0xad3f9292, 0xbc219d9d, 0x48703838, 0x04f1f5f5, 0xdf63bcbc, 0xc177b6b6, 0x75afdada, 0x63422121, 0x30201010, 0x1ae5ffff, 0x0efdf3f3, 0x6dbfd2d2, 0x4c81cdcd, 0x14180c0c, 0x35261313, 0x2fc3ecec, 0xe1be5f5f, 0xa2359797, 0xcc884444, 0x392e1717, 0x5793c4c4, 0xf255a7a7, 0x82fc7e7e, 0x477a3d3d, 0xacc86464, 0xe7ba5d5d, 0x2b321919, 0x95e67373, 0xa0c06060, 0x98198181, 0xd19e4f4f, 0x7fa3dcdc, 0x66442222, 0x7e542a2a, 0xab3b9090, 0x830b8888, 0xca8c4646, 0x29c7eeee, 0xd36bb8b8, 0x3c281414, 0x79a7dede, 0xe2bc5e5e, 0x1d160b0b, 0x76addbdb, 0x3bdbe0e0, 0x56643232, 0x4e743a3a, 0x1e140a0a, 0xdb924949, 0x0a0c0606, 0x6c482424, 0xe4b85c5c, 0x5d9fc2c2, 0x6ebdd3d3, 0xef43acac, 0xa6c46262, 0xa8399191, 0xa4319595, 0x37d3e4e4, 0x8bf27979, 0x32d5e7e7, 0x438bc8c8, 0x596e3737, 0xb7da6d6d, 0x8c018d8d, 0x64b1d5d5, 0xd29c4e4e, 0xe049a9a9, 0xb4d86c6c, 0xfaac5656, 0x07f3f4f4, 0x25cfeaea, 0xafca6565, 0x8ef47a7a, 0xe947aeae, 0x18100808, 0xd56fbaba, 0x88f07878, 0x6f4a2525, 0x725c2e2e, 0x24381c1c, 0xf157a6a6, 0xc773b4b4, 0x5197c6c6, 0x23cbe8e8, 0x7ca1dddd, 0x9ce87474, 0x213e1f1f, 0xdd964b4b, 0xdc61bdbd, 0x860d8b8b, 0x850f8a8a, 0x90e07070, 0x427c3e3e, 0xc471b5b5, 0xaacc6666, 0xd8904848, 0x05060303, 0x01f7f6f6, 0x121c0e0e, 0xa3c26161, 0x5f6a3535, 0xf9ae5757, 0xd069b9b9, 0x91178686, 0x5899c1c1, 0x273a1d1d, 0xb9279e9e, 0x38d9e1e1, 0x13ebf8f8, 0xb32b9898, 0x33221111, 0xbbd26969, 0x70a9d9d9, 0x89078e8e, 0xa7339494, 0xb62d9b9b, 0x223c1e1e, 0x92158787, 0x20c9e9e9, 0x4987cece, 0xffaa5555, 0x78502828, 0x7aa5dfdf, 0x8f038c8c, 0xf859a1a1, 0x80098989, 0x171a0d0d, 0xda65bfbf, 0x31d7e6e6, 0xc6844242, 0xb8d06868, 0xc3824141, 0xb0299999, 0x775a2d2d, 0x111e0f0f, 0xcb7bb0b0, 0xfca85454, 0xd66dbbbb, 0x3a2c1616];
    var T3 = [0x63a5c663, 0x7c84f87c, 0x7799ee77, 0x7b8df67b, 0xf20dfff2, 0x6bbdd66b, 0x6fb1de6f, 0xc55491c5, 0x30506030, 0x01030201, 0x67a9ce67, 0x2b7d562b, 0xfe19e7fe, 0xd762b5d7, 0xabe64dab, 0x769aec76, 0xca458fca, 0x829d1f82, 0xc94089c9, 0x7d87fa7d, 0xfa15effa, 0x59ebb259, 0x47c98e47, 0xf00bfbf0, 0xadec41ad, 0xd467b3d4, 0xa2fd5fa2, 0xafea45af, 0x9cbf239c, 0xa4f753a4, 0x7296e472, 0xc05b9bc0, 0xb7c275b7, 0xfd1ce1fd, 0x93ae3d93, 0x266a4c26, 0x365a6c36, 0x3f417e3f, 0xf702f5f7, 0xcc4f83cc, 0x345c6834, 0xa5f451a5, 0xe534d1e5, 0xf108f9f1, 0x7193e271, 0xd873abd8, 0x31536231, 0x153f2a15, 0x040c0804, 0xc75295c7, 0x23654623, 0xc35e9dc3, 0x18283018, 0x96a13796, 0x050f0a05, 0x9ab52f9a, 0x07090e07, 0x12362412, 0x809b1b80, 0xe23ddfe2, 0xeb26cdeb, 0x27694e27, 0xb2cd7fb2, 0x759fea75, 0x091b1209, 0x839e1d83, 0x2c74582c, 0x1a2e341a, 0x1b2d361b, 0x6eb2dc6e, 0x5aeeb45a, 0xa0fb5ba0, 0x52f6a452, 0x3b4d763b, 0xd661b7d6, 0xb3ce7db3, 0x297b5229, 0xe33edde3, 0x2f715e2f, 0x84971384, 0x53f5a653, 0xd168b9d1, 0x00000000, 0xed2cc1ed, 0x20604020, 0xfc1fe3fc, 0xb1c879b1, 0x5bedb65b, 0x6abed46a, 0xcb468dcb, 0xbed967be, 0x394b7239, 0x4ade944a, 0x4cd4984c, 0x58e8b058, 0xcf4a85cf, 0xd06bbbd0, 0xef2ac5ef, 0xaae54faa, 0xfb16edfb, 0x43c58643, 0x4dd79a4d, 0x33556633, 0x85941185, 0x45cf8a45, 0xf910e9f9, 0x02060402, 0x7f81fe7f, 0x50f0a050, 0x3c44783c, 0x9fba259f, 0xa8e34ba8, 0x51f3a251, 0xa3fe5da3, 0x40c08040, 0x8f8a058f, 0x92ad3f92, 0x9dbc219d, 0x38487038, 0xf504f1f5, 0xbcdf63bc, 0xb6c177b6, 0xda75afda, 0x21634221, 0x10302010, 0xff1ae5ff, 0xf30efdf3, 0xd26dbfd2, 0xcd4c81cd, 0x0c14180c, 0x13352613, 0xec2fc3ec, 0x5fe1be5f, 0x97a23597, 0x44cc8844, 0x17392e17, 0xc45793c4, 0xa7f255a7, 0x7e82fc7e, 0x3d477a3d, 0x64acc864, 0x5de7ba5d, 0x192b3219, 0x7395e673, 0x60a0c060, 0x81981981, 0x4fd19e4f, 0xdc7fa3dc, 0x22664422, 0x2a7e542a, 0x90ab3b90, 0x88830b88, 0x46ca8c46, 0xee29c7ee, 0xb8d36bb8, 0x143c2814, 0xde79a7de, 0x5ee2bc5e, 0x0b1d160b, 0xdb76addb, 0xe03bdbe0, 0x32566432, 0x3a4e743a, 0x0a1e140a, 0x49db9249, 0x060a0c06, 0x246c4824, 0x5ce4b85c, 0xc25d9fc2, 0xd36ebdd3, 0xacef43ac, 0x62a6c462, 0x91a83991, 0x95a43195, 0xe437d3e4, 0x798bf279, 0xe732d5e7, 0xc8438bc8, 0x37596e37, 0x6db7da6d, 0x8d8c018d, 0xd564b1d5, 0x4ed29c4e, 0xa9e049a9, 0x6cb4d86c, 0x56faac56, 0xf407f3f4, 0xea25cfea, 0x65afca65, 0x7a8ef47a, 0xaee947ae, 0x08181008, 0xbad56fba, 0x7888f078, 0x256f4a25, 0x2e725c2e, 0x1c24381c, 0xa6f157a6, 0xb4c773b4, 0xc65197c6, 0xe823cbe8, 0xdd7ca1dd, 0x749ce874, 0x1f213e1f, 0x4bdd964b, 0xbddc61bd, 0x8b860d8b, 0x8a850f8a, 0x7090e070, 0x3e427c3e, 0xb5c471b5, 0x66aacc66, 0x48d89048, 0x03050603, 0xf601f7f6, 0x0e121c0e, 0x61a3c261, 0x355f6a35, 0x57f9ae57, 0xb9d069b9, 0x86911786, 0xc15899c1, 0x1d273a1d, 0x9eb9279e, 0xe138d9e1, 0xf813ebf8, 0x98b32b98, 0x11332211, 0x69bbd269, 0xd970a9d9, 0x8e89078e, 0x94a73394, 0x9bb62d9b, 0x1e223c1e, 0x87921587, 0xe920c9e9, 0xce4987ce, 0x55ffaa55, 0x28785028, 0xdf7aa5df, 0x8c8f038c, 0xa1f859a1, 0x89800989, 0x0d171a0d, 0xbfda65bf, 0xe631d7e6, 0x42c68442, 0x68b8d068, 0x41c38241, 0x99b02999, 0x2d775a2d, 0x0f111e0f, 0xb0cb7bb0, 0x54fca854, 0xbbd66dbb, 0x163a2c16];
    var T4 = [0x6363a5c6, 0x7c7c84f8, 0x777799ee, 0x7b7b8df6, 0xf2f20dff, 0x6b6bbdd6, 0x6f6fb1de, 0xc5c55491, 0x30305060, 0x01010302, 0x6767a9ce, 0x2b2b7d56, 0xfefe19e7, 0xd7d762b5, 0xababe64d, 0x76769aec, 0xcaca458f, 0x82829d1f, 0xc9c94089, 0x7d7d87fa, 0xfafa15ef, 0x5959ebb2, 0x4747c98e, 0xf0f00bfb, 0xadadec41, 0xd4d467b3, 0xa2a2fd5f, 0xafafea45, 0x9c9cbf23, 0xa4a4f753, 0x727296e4, 0xc0c05b9b, 0xb7b7c275, 0xfdfd1ce1, 0x9393ae3d, 0x26266a4c, 0x36365a6c, 0x3f3f417e, 0xf7f702f5, 0xcccc4f83, 0x34345c68, 0xa5a5f451, 0xe5e534d1, 0xf1f108f9, 0x717193e2, 0xd8d873ab, 0x31315362, 0x15153f2a, 0x04040c08, 0xc7c75295, 0x23236546, 0xc3c35e9d, 0x18182830, 0x9696a137, 0x05050f0a, 0x9a9ab52f, 0x0707090e, 0x12123624, 0x80809b1b, 0xe2e23ddf, 0xebeb26cd, 0x2727694e, 0xb2b2cd7f, 0x75759fea, 0x09091b12, 0x83839e1d, 0x2c2c7458, 0x1a1a2e34, 0x1b1b2d36, 0x6e6eb2dc, 0x5a5aeeb4, 0xa0a0fb5b, 0x5252f6a4, 0x3b3b4d76, 0xd6d661b7, 0xb3b3ce7d, 0x29297b52, 0xe3e33edd, 0x2f2f715e, 0x84849713, 0x5353f5a6, 0xd1d168b9, 0x00000000, 0xeded2cc1, 0x20206040, 0xfcfc1fe3, 0xb1b1c879, 0x5b5bedb6, 0x6a6abed4, 0xcbcb468d, 0xbebed967, 0x39394b72, 0x4a4ade94, 0x4c4cd498, 0x5858e8b0, 0xcfcf4a85, 0xd0d06bbb, 0xefef2ac5, 0xaaaae54f, 0xfbfb16ed, 0x4343c586, 0x4d4dd79a, 0x33335566, 0x85859411, 0x4545cf8a, 0xf9f910e9, 0x02020604, 0x7f7f81fe, 0x5050f0a0, 0x3c3c4478, 0x9f9fba25, 0xa8a8e34b, 0x5151f3a2, 0xa3a3fe5d, 0x4040c080, 0x8f8f8a05, 0x9292ad3f, 0x9d9dbc21, 0x38384870, 0xf5f504f1, 0xbcbcdf63, 0xb6b6c177, 0xdada75af, 0x21216342, 0x10103020, 0xffff1ae5, 0xf3f30efd, 0xd2d26dbf, 0xcdcd4c81, 0x0c0c1418, 0x13133526, 0xecec2fc3, 0x5f5fe1be, 0x9797a235, 0x4444cc88, 0x1717392e, 0xc4c45793, 0xa7a7f255, 0x7e7e82fc, 0x3d3d477a, 0x6464acc8, 0x5d5de7ba, 0x19192b32, 0x737395e6, 0x6060a0c0, 0x81819819, 0x4f4fd19e, 0xdcdc7fa3, 0x22226644, 0x2a2a7e54, 0x9090ab3b, 0x8888830b, 0x4646ca8c, 0xeeee29c7, 0xb8b8d36b, 0x14143c28, 0xdede79a7, 0x5e5ee2bc, 0x0b0b1d16, 0xdbdb76ad, 0xe0e03bdb, 0x32325664, 0x3a3a4e74, 0x0a0a1e14, 0x4949db92, 0x06060a0c, 0x24246c48, 0x5c5ce4b8, 0xc2c25d9f, 0xd3d36ebd, 0xacacef43, 0x6262a6c4, 0x9191a839, 0x9595a431, 0xe4e437d3, 0x79798bf2, 0xe7e732d5, 0xc8c8438b, 0x3737596e, 0x6d6db7da, 0x8d8d8c01, 0xd5d564b1, 0x4e4ed29c, 0xa9a9e049, 0x6c6cb4d8, 0x5656faac, 0xf4f407f3, 0xeaea25cf, 0x6565afca, 0x7a7a8ef4, 0xaeaee947, 0x08081810, 0xbabad56f, 0x787888f0, 0x25256f4a, 0x2e2e725c, 0x1c1c2438, 0xa6a6f157, 0xb4b4c773, 0xc6c65197, 0xe8e823cb, 0xdddd7ca1, 0x74749ce8, 0x1f1f213e, 0x4b4bdd96, 0xbdbddc61, 0x8b8b860d, 0x8a8a850f, 0x707090e0, 0x3e3e427c, 0xb5b5c471, 0x6666aacc, 0x4848d890, 0x03030506, 0xf6f601f7, 0x0e0e121c, 0x6161a3c2, 0x35355f6a, 0x5757f9ae, 0xb9b9d069, 0x86869117, 0xc1c15899, 0x1d1d273a, 0x9e9eb927, 0xe1e138d9, 0xf8f813eb, 0x9898b32b, 0x11113322, 0x6969bbd2, 0xd9d970a9, 0x8e8e8907, 0x9494a733, 0x9b9bb62d, 0x1e1e223c, 0x87879215, 0xe9e920c9, 0xcece4987, 0x5555ffaa, 0x28287850, 0xdfdf7aa5, 0x8c8c8f03, 0xa1a1f859, 0x89898009, 0x0d0d171a, 0xbfbfda65, 0xe6e631d7, 0x4242c684, 0x6868b8d0, 0x4141c382, 0x9999b029, 0x2d2d775a, 0x0f0f111e, 0xb0b0cb7b, 0x5454fca8, 0xbbbbd66d, 0x16163a2c];

    // Transformations for decryption
    var T5 = [0x51f4a750, 0x7e416553, 0x1a17a4c3, 0x3a275e96, 0x3bab6bcb, 0x1f9d45f1, 0xacfa58ab, 0x4be30393, 0x2030fa55, 0xad766df6, 0x88cc7691, 0xf5024c25, 0x4fe5d7fc, 0xc52acbd7, 0x26354480, 0xb562a38f, 0xdeb15a49, 0x25ba1b67, 0x45ea0e98, 0x5dfec0e1, 0xc32f7502, 0x814cf012, 0x8d4697a3, 0x6bd3f9c6, 0x038f5fe7, 0x15929c95, 0xbf6d7aeb, 0x955259da, 0xd4be832d, 0x587421d3, 0x49e06929, 0x8ec9c844, 0x75c2896a, 0xf48e7978, 0x99583e6b, 0x27b971dd, 0xbee14fb6, 0xf088ad17, 0xc920ac66, 0x7dce3ab4, 0x63df4a18, 0xe51a3182, 0x97513360, 0x62537f45, 0xb16477e0, 0xbb6bae84, 0xfe81a01c, 0xf9082b94, 0x70486858, 0x8f45fd19, 0x94de6c87, 0x527bf8b7, 0xab73d323, 0x724b02e2, 0xe31f8f57, 0x6655ab2a, 0xb2eb2807, 0x2fb5c203, 0x86c57b9a, 0xd33708a5, 0x302887f2, 0x23bfa5b2, 0x02036aba, 0xed16825c, 0x8acf1c2b, 0xa779b492, 0xf307f2f0, 0x4e69e2a1, 0x65daf4cd, 0x0605bed5, 0xd134621f, 0xc4a6fe8a, 0x342e539d, 0xa2f355a0, 0x058ae132, 0xa4f6eb75, 0x0b83ec39, 0x4060efaa, 0x5e719f06, 0xbd6e1051, 0x3e218af9, 0x96dd063d, 0xdd3e05ae, 0x4de6bd46, 0x91548db5, 0x71c45d05, 0x0406d46f, 0x605015ff, 0x1998fb24, 0xd6bde997, 0x894043cc, 0x67d99e77, 0xb0e842bd, 0x07898b88, 0xe7195b38, 0x79c8eedb, 0xa17c0a47, 0x7c420fe9, 0xf8841ec9, 0x00000000, 0x09808683, 0x322bed48, 0x1e1170ac, 0x6c5a724e, 0xfd0efffb, 0x0f853856, 0x3daed51e, 0x362d3927, 0x0a0fd964, 0x685ca621, 0x9b5b54d1, 0x24362e3a, 0x0c0a67b1, 0x9357e70f, 0xb4ee96d2, 0x1b9b919e, 0x80c0c54f, 0x61dc20a2, 0x5a774b69, 0x1c121a16, 0xe293ba0a, 0xc0a02ae5, 0x3c22e043, 0x121b171d, 0x0e090d0b, 0xf28bc7ad, 0x2db6a8b9, 0x141ea9c8, 0x57f11985, 0xaf75074c, 0xee99ddbb, 0xa37f60fd, 0xf701269f, 0x5c72f5bc, 0x44663bc5, 0x5bfb7e34, 0x8b432976, 0xcb23c6dc, 0xb6edfc68, 0xb8e4f163, 0xd731dcca, 0x42638510, 0x13972240, 0x84c61120, 0x854a247d, 0xd2bb3df8, 0xaef93211, 0xc729a16d, 0x1d9e2f4b, 0xdcb230f3, 0x0d8652ec, 0x77c1e3d0, 0x2bb3166c, 0xa970b999, 0x119448fa, 0x47e96422, 0xa8fc8cc4, 0xa0f03f1a, 0x567d2cd8, 0x223390ef, 0x87494ec7, 0xd938d1c1, 0x8ccaa2fe, 0x98d40b36, 0xa6f581cf, 0xa57ade28, 0xdab78e26, 0x3fadbfa4, 0x2c3a9de4, 0x5078920d, 0x6a5fcc9b, 0x547e4662, 0xf68d13c2, 0x90d8b8e8, 0x2e39f75e, 0x82c3aff5, 0x9f5d80be, 0x69d0937c, 0x6fd52da9, 0xcf2512b3, 0xc8ac993b, 0x10187da7, 0xe89c636e, 0xdb3bbb7b, 0xcd267809, 0x6e5918f4, 0xec9ab701, 0x834f9aa8, 0xe6956e65, 0xaaffe67e, 0x21bccf08, 0xef15e8e6, 0xbae79bd9, 0x4a6f36ce, 0xea9f09d4, 0x29b07cd6, 0x31a4b2af, 0x2a3f2331, 0xc6a59430, 0x35a266c0, 0x744ebc37, 0xfc82caa6, 0xe090d0b0, 0x33a7d815, 0xf104984a, 0x41ecdaf7, 0x7fcd500e, 0x1791f62f, 0x764dd68d, 0x43efb04d, 0xccaa4d54, 0xe49604df, 0x9ed1b5e3, 0x4c6a881b, 0xc12c1fb8, 0x4665517f, 0x9d5eea04, 0x018c355d, 0xfa877473, 0xfb0b412e, 0xb3671d5a, 0x92dbd252, 0xe9105633, 0x6dd64713, 0x9ad7618c, 0x37a10c7a, 0x59f8148e, 0xeb133c89, 0xcea927ee, 0xb761c935, 0xe11ce5ed, 0x7a47b13c, 0x9cd2df59, 0x55f2733f, 0x1814ce79, 0x73c737bf, 0x53f7cdea, 0x5ffdaa5b, 0xdf3d6f14, 0x7844db86, 0xcaaff381, 0xb968c43e, 0x3824342c, 0xc2a3405f, 0x161dc372, 0xbce2250c, 0x283c498b, 0xff0d9541, 0x39a80171, 0x080cb3de, 0xd8b4e49c, 0x6456c190, 0x7bcb8461, 0xd532b670, 0x486c5c74, 0xd0b85742];
    var T6 = [0x5051f4a7, 0x537e4165, 0xc31a17a4, 0x963a275e, 0xcb3bab6b, 0xf11f9d45, 0xabacfa58, 0x934be303, 0x552030fa, 0xf6ad766d, 0x9188cc76, 0x25f5024c, 0xfc4fe5d7, 0xd7c52acb, 0x80263544, 0x8fb562a3, 0x49deb15a, 0x6725ba1b, 0x9845ea0e, 0xe15dfec0, 0x02c32f75, 0x12814cf0, 0xa38d4697, 0xc66bd3f9, 0xe7038f5f, 0x9515929c, 0xebbf6d7a, 0xda955259, 0x2dd4be83, 0xd3587421, 0x2949e069, 0x448ec9c8, 0x6a75c289, 0x78f48e79, 0x6b99583e, 0xdd27b971, 0xb6bee14f, 0x17f088ad, 0x66c920ac, 0xb47dce3a, 0x1863df4a, 0x82e51a31, 0x60975133, 0x4562537f, 0xe0b16477, 0x84bb6bae, 0x1cfe81a0, 0x94f9082b, 0x58704868, 0x198f45fd, 0x8794de6c, 0xb7527bf8, 0x23ab73d3, 0xe2724b02, 0x57e31f8f, 0x2a6655ab, 0x07b2eb28, 0x032fb5c2, 0x9a86c57b, 0xa5d33708, 0xf2302887, 0xb223bfa5, 0xba02036a, 0x5ced1682, 0x2b8acf1c, 0x92a779b4, 0xf0f307f2, 0xa14e69e2, 0xcd65daf4, 0xd50605be, 0x1fd13462, 0x8ac4a6fe, 0x9d342e53, 0xa0a2f355, 0x32058ae1, 0x75a4f6eb, 0x390b83ec, 0xaa4060ef, 0x065e719f, 0x51bd6e10, 0xf93e218a, 0x3d96dd06, 0xaedd3e05, 0x464de6bd, 0xb591548d, 0x0571c45d, 0x6f0406d4, 0xff605015, 0x241998fb, 0x97d6bde9, 0xcc894043, 0x7767d99e, 0xbdb0e842, 0x8807898b, 0x38e7195b, 0xdb79c8ee, 0x47a17c0a, 0xe97c420f, 0xc9f8841e, 0x00000000, 0x83098086, 0x48322bed, 0xac1e1170, 0x4e6c5a72, 0xfbfd0eff, 0x560f8538, 0x1e3daed5, 0x27362d39, 0x640a0fd9, 0x21685ca6, 0xd19b5b54, 0x3a24362e, 0xb10c0a67, 0x0f9357e7, 0xd2b4ee96, 0x9e1b9b91, 0x4f80c0c5, 0xa261dc20, 0x695a774b, 0x161c121a, 0x0ae293ba, 0xe5c0a02a, 0x433c22e0, 0x1d121b17, 0x0b0e090d, 0xadf28bc7, 0xb92db6a8, 0xc8141ea9, 0x8557f119, 0x4caf7507, 0xbbee99dd, 0xfda37f60, 0x9ff70126, 0xbc5c72f5, 0xc544663b, 0x345bfb7e, 0x768b4329, 0xdccb23c6, 0x68b6edfc, 0x63b8e4f1, 0xcad731dc, 0x10426385, 0x40139722, 0x2084c611, 0x7d854a24, 0xf8d2bb3d, 0x11aef932, 0x6dc729a1, 0x4b1d9e2f, 0xf3dcb230, 0xec0d8652, 0xd077c1e3, 0x6c2bb316, 0x99a970b9, 0xfa119448, 0x2247e964, 0xc4a8fc8c, 0x1aa0f03f, 0xd8567d2c, 0xef223390, 0xc787494e, 0xc1d938d1, 0xfe8ccaa2, 0x3698d40b, 0xcfa6f581, 0x28a57ade, 0x26dab78e, 0xa43fadbf, 0xe42c3a9d, 0x0d507892, 0x9b6a5fcc, 0x62547e46, 0xc2f68d13, 0xe890d8b8, 0x5e2e39f7, 0xf582c3af, 0xbe9f5d80, 0x7c69d093, 0xa96fd52d, 0xb3cf2512, 0x3bc8ac99, 0xa710187d, 0x6ee89c63, 0x7bdb3bbb, 0x09cd2678, 0xf46e5918, 0x01ec9ab7, 0xa8834f9a, 0x65e6956e, 0x7eaaffe6, 0x0821bccf, 0xe6ef15e8, 0xd9bae79b, 0xce4a6f36, 0xd4ea9f09, 0xd629b07c, 0xaf31a4b2, 0x312a3f23, 0x30c6a594, 0xc035a266, 0x37744ebc, 0xa6fc82ca, 0xb0e090d0, 0x1533a7d8, 0x4af10498, 0xf741ecda, 0x0e7fcd50, 0x2f1791f6, 0x8d764dd6, 0x4d43efb0, 0x54ccaa4d, 0xdfe49604, 0xe39ed1b5, 0x1b4c6a88, 0xb8c12c1f, 0x7f466551, 0x049d5eea, 0x5d018c35, 0x73fa8774, 0x2efb0b41, 0x5ab3671d, 0x5292dbd2, 0x33e91056, 0x136dd647, 0x8c9ad761, 0x7a37a10c, 0x8e59f814, 0x89eb133c, 0xeecea927, 0x35b761c9, 0xede11ce5, 0x3c7a47b1, 0x599cd2df, 0x3f55f273, 0x791814ce, 0xbf73c737, 0xea53f7cd, 0x5b5ffdaa, 0x14df3d6f, 0x867844db, 0x81caaff3, 0x3eb968c4, 0x2c382434, 0x5fc2a340, 0x72161dc3, 0x0cbce225, 0x8b283c49, 0x41ff0d95, 0x7139a801, 0xde080cb3, 0x9cd8b4e4, 0x906456c1, 0x617bcb84, 0x70d532b6, 0x74486c5c, 0x42d0b857];
    var T7 = [0xa75051f4, 0x65537e41, 0xa4c31a17, 0x5e963a27, 0x6bcb3bab, 0x45f11f9d, 0x58abacfa, 0x03934be3, 0xfa552030, 0x6df6ad76, 0x769188cc, 0x4c25f502, 0xd7fc4fe5, 0xcbd7c52a, 0x44802635, 0xa38fb562, 0x5a49deb1, 0x1b6725ba, 0x0e9845ea, 0xc0e15dfe, 0x7502c32f, 0xf012814c, 0x97a38d46, 0xf9c66bd3, 0x5fe7038f, 0x9c951592, 0x7aebbf6d, 0x59da9552, 0x832dd4be, 0x21d35874, 0x692949e0, 0xc8448ec9, 0x896a75c2, 0x7978f48e, 0x3e6b9958, 0x71dd27b9, 0x4fb6bee1, 0xad17f088, 0xac66c920, 0x3ab47dce, 0x4a1863df, 0x3182e51a, 0x33609751, 0x7f456253, 0x77e0b164, 0xae84bb6b, 0xa01cfe81, 0x2b94f908, 0x68587048, 0xfd198f45, 0x6c8794de, 0xf8b7527b, 0xd323ab73, 0x02e2724b, 0x8f57e31f, 0xab2a6655, 0x2807b2eb, 0xc2032fb5, 0x7b9a86c5, 0x08a5d337, 0x87f23028, 0xa5b223bf, 0x6aba0203, 0x825ced16, 0x1c2b8acf, 0xb492a779, 0xf2f0f307, 0xe2a14e69, 0xf4cd65da, 0xbed50605, 0x621fd134, 0xfe8ac4a6, 0x539d342e, 0x55a0a2f3, 0xe132058a, 0xeb75a4f6, 0xec390b83, 0xefaa4060, 0x9f065e71, 0x1051bd6e, 0x8af93e21, 0x063d96dd, 0x05aedd3e, 0xbd464de6, 0x8db59154, 0x5d0571c4, 0xd46f0406, 0x15ff6050, 0xfb241998, 0xe997d6bd, 0x43cc8940, 0x9e7767d9, 0x42bdb0e8, 0x8b880789, 0x5b38e719, 0xeedb79c8, 0x0a47a17c, 0x0fe97c42, 0x1ec9f884, 0x00000000, 0x86830980, 0xed48322b, 0x70ac1e11, 0x724e6c5a, 0xfffbfd0e, 0x38560f85, 0xd51e3dae, 0x3927362d, 0xd9640a0f, 0xa621685c, 0x54d19b5b, 0x2e3a2436, 0x67b10c0a, 0xe70f9357, 0x96d2b4ee, 0x919e1b9b, 0xc54f80c0, 0x20a261dc, 0x4b695a77, 0x1a161c12, 0xba0ae293, 0x2ae5c0a0, 0xe0433c22, 0x171d121b, 0x0d0b0e09, 0xc7adf28b, 0xa8b92db6, 0xa9c8141e, 0x198557f1, 0x074caf75, 0xddbbee99, 0x60fda37f, 0x269ff701, 0xf5bc5c72, 0x3bc54466, 0x7e345bfb, 0x29768b43, 0xc6dccb23, 0xfc68b6ed, 0xf163b8e4, 0xdccad731, 0x85104263, 0x22401397, 0x112084c6, 0x247d854a, 0x3df8d2bb, 0x3211aef9, 0xa16dc729, 0x2f4b1d9e, 0x30f3dcb2, 0x52ec0d86, 0xe3d077c1, 0x166c2bb3, 0xb999a970, 0x48fa1194, 0x642247e9, 0x8cc4a8fc, 0x3f1aa0f0, 0x2cd8567d, 0x90ef2233, 0x4ec78749, 0xd1c1d938, 0xa2fe8cca, 0x0b3698d4, 0x81cfa6f5, 0xde28a57a, 0x8e26dab7, 0xbfa43fad, 0x9de42c3a, 0x920d5078, 0xcc9b6a5f, 0x4662547e, 0x13c2f68d, 0xb8e890d8, 0xf75e2e39, 0xaff582c3, 0x80be9f5d, 0x937c69d0, 0x2da96fd5, 0x12b3cf25, 0x993bc8ac, 0x7da71018, 0x636ee89c, 0xbb7bdb3b, 0x7809cd26, 0x18f46e59, 0xb701ec9a, 0x9aa8834f, 0x6e65e695, 0xe67eaaff, 0xcf0821bc, 0xe8e6ef15, 0x9bd9bae7, 0x36ce4a6f, 0x09d4ea9f, 0x7cd629b0, 0xb2af31a4, 0x23312a3f, 0x9430c6a5, 0x66c035a2, 0xbc37744e, 0xcaa6fc82, 0xd0b0e090, 0xd81533a7, 0x984af104, 0xdaf741ec, 0x500e7fcd, 0xf62f1791, 0xd68d764d, 0xb04d43ef, 0x4d54ccaa, 0x04dfe496, 0xb5e39ed1, 0x881b4c6a, 0x1fb8c12c, 0x517f4665, 0xea049d5e, 0x355d018c, 0x7473fa87, 0x412efb0b, 0x1d5ab367, 0xd25292db, 0x5633e910, 0x47136dd6, 0x618c9ad7, 0x0c7a37a1, 0x148e59f8, 0x3c89eb13, 0x27eecea9, 0xc935b761, 0xe5ede11c, 0xb13c7a47, 0xdf599cd2, 0x733f55f2, 0xce791814, 0x37bf73c7, 0xcdea53f7, 0xaa5b5ffd, 0x6f14df3d, 0xdb867844, 0xf381caaf, 0xc43eb968, 0x342c3824, 0x405fc2a3, 0xc372161d, 0x250cbce2, 0x498b283c, 0x9541ff0d, 0x017139a8, 0xb3de080c, 0xe49cd8b4, 0xc1906456, 0x84617bcb, 0xb670d532, 0x5c74486c, 0x5742d0b8];
    var T8 = [0xf4a75051, 0x4165537e, 0x17a4c31a, 0x275e963a, 0xab6bcb3b, 0x9d45f11f, 0xfa58abac, 0xe303934b, 0x30fa5520, 0x766df6ad, 0xcc769188, 0x024c25f5, 0xe5d7fc4f, 0x2acbd7c5, 0x35448026, 0x62a38fb5, 0xb15a49de, 0xba1b6725, 0xea0e9845, 0xfec0e15d, 0x2f7502c3, 0x4cf01281, 0x4697a38d, 0xd3f9c66b, 0x8f5fe703, 0x929c9515, 0x6d7aebbf, 0x5259da95, 0xbe832dd4, 0x7421d358, 0xe0692949, 0xc9c8448e, 0xc2896a75, 0x8e7978f4, 0x583e6b99, 0xb971dd27, 0xe14fb6be, 0x88ad17f0, 0x20ac66c9, 0xce3ab47d, 0xdf4a1863, 0x1a3182e5, 0x51336097, 0x537f4562, 0x6477e0b1, 0x6bae84bb, 0x81a01cfe, 0x082b94f9, 0x48685870, 0x45fd198f, 0xde6c8794, 0x7bf8b752, 0x73d323ab, 0x4b02e272, 0x1f8f57e3, 0x55ab2a66, 0xeb2807b2, 0xb5c2032f, 0xc57b9a86, 0x3708a5d3, 0x2887f230, 0xbfa5b223, 0x036aba02, 0x16825ced, 0xcf1c2b8a, 0x79b492a7, 0x07f2f0f3, 0x69e2a14e, 0xdaf4cd65, 0x05bed506, 0x34621fd1, 0xa6fe8ac4, 0x2e539d34, 0xf355a0a2, 0x8ae13205, 0xf6eb75a4, 0x83ec390b, 0x60efaa40, 0x719f065e, 0x6e1051bd, 0x218af93e, 0xdd063d96, 0x3e05aedd, 0xe6bd464d, 0x548db591, 0xc45d0571, 0x06d46f04, 0x5015ff60, 0x98fb2419, 0xbde997d6, 0x4043cc89, 0xd99e7767, 0xe842bdb0, 0x898b8807, 0x195b38e7, 0xc8eedb79, 0x7c0a47a1, 0x420fe97c, 0x841ec9f8, 0x00000000, 0x80868309, 0x2bed4832, 0x1170ac1e, 0x5a724e6c, 0x0efffbfd, 0x8538560f, 0xaed51e3d, 0x2d392736, 0x0fd9640a, 0x5ca62168, 0x5b54d19b, 0x362e3a24, 0x0a67b10c, 0x57e70f93, 0xee96d2b4, 0x9b919e1b, 0xc0c54f80, 0xdc20a261, 0x774b695a, 0x121a161c, 0x93ba0ae2, 0xa02ae5c0, 0x22e0433c, 0x1b171d12, 0x090d0b0e, 0x8bc7adf2, 0xb6a8b92d, 0x1ea9c814, 0xf1198557, 0x75074caf, 0x99ddbbee, 0x7f60fda3, 0x01269ff7, 0x72f5bc5c, 0x663bc544, 0xfb7e345b, 0x4329768b, 0x23c6dccb, 0xedfc68b6, 0xe4f163b8, 0x31dccad7, 0x63851042, 0x97224013, 0xc6112084, 0x4a247d85, 0xbb3df8d2, 0xf93211ae, 0x29a16dc7, 0x9e2f4b1d, 0xb230f3dc, 0x8652ec0d, 0xc1e3d077, 0xb3166c2b, 0x70b999a9, 0x9448fa11, 0xe9642247, 0xfc8cc4a8, 0xf03f1aa0, 0x7d2cd856, 0x3390ef22, 0x494ec787, 0x38d1c1d9, 0xcaa2fe8c, 0xd40b3698, 0xf581cfa6, 0x7ade28a5, 0xb78e26da, 0xadbfa43f, 0x3a9de42c, 0x78920d50, 0x5fcc9b6a, 0x7e466254, 0x8d13c2f6, 0xd8b8e890, 0x39f75e2e, 0xc3aff582, 0x5d80be9f, 0xd0937c69, 0xd52da96f, 0x2512b3cf, 0xac993bc8, 0x187da710, 0x9c636ee8, 0x3bbb7bdb, 0x267809cd, 0x5918f46e, 0x9ab701ec, 0x4f9aa883, 0x956e65e6, 0xffe67eaa, 0xbccf0821, 0x15e8e6ef, 0xe79bd9ba, 0x6f36ce4a, 0x9f09d4ea, 0xb07cd629, 0xa4b2af31, 0x3f23312a, 0xa59430c6, 0xa266c035, 0x4ebc3774, 0x82caa6fc, 0x90d0b0e0, 0xa7d81533, 0x04984af1, 0xecdaf741, 0xcd500e7f, 0x91f62f17, 0x4dd68d76, 0xefb04d43, 0xaa4d54cc, 0x9604dfe4, 0xd1b5e39e, 0x6a881b4c, 0x2c1fb8c1, 0x65517f46, 0x5eea049d, 0x8c355d01, 0x877473fa, 0x0b412efb, 0x671d5ab3, 0xdbd25292, 0x105633e9, 0xd647136d, 0xd7618c9a, 0xa10c7a37, 0xf8148e59, 0x133c89eb, 0xa927eece, 0x61c935b7, 0x1ce5ede1, 0x47b13c7a, 0xd2df599c, 0xf2733f55, 0x14ce7918, 0xc737bf73, 0xf7cdea53, 0xfdaa5b5f, 0x3d6f14df, 0x44db8678, 0xaff381ca, 0x68c43eb9, 0x24342c38, 0xa3405fc2, 0x1dc37216, 0xe2250cbc, 0x3c498b28, 0x0d9541ff, 0xa8017139, 0x0cb3de08, 0xb4e49cd8, 0x56c19064, 0xcb84617b, 0x32b670d5, 0x6c5c7448, 0xb85742d0];

    // Transformations for decryption key expansion
    var U1 = [0x00000000, 0x0e090d0b, 0x1c121a16, 0x121b171d, 0x3824342c, 0x362d3927, 0x24362e3a, 0x2a3f2331, 0x70486858, 0x7e416553, 0x6c5a724e, 0x62537f45, 0x486c5c74, 0x4665517f, 0x547e4662, 0x5a774b69, 0xe090d0b0, 0xee99ddbb, 0xfc82caa6, 0xf28bc7ad, 0xd8b4e49c, 0xd6bde997, 0xc4a6fe8a, 0xcaaff381, 0x90d8b8e8, 0x9ed1b5e3, 0x8ccaa2fe, 0x82c3aff5, 0xa8fc8cc4, 0xa6f581cf, 0xb4ee96d2, 0xbae79bd9, 0xdb3bbb7b, 0xd532b670, 0xc729a16d, 0xc920ac66, 0xe31f8f57, 0xed16825c, 0xff0d9541, 0xf104984a, 0xab73d323, 0xa57ade28, 0xb761c935, 0xb968c43e, 0x9357e70f, 0x9d5eea04, 0x8f45fd19, 0x814cf012, 0x3bab6bcb, 0x35a266c0, 0x27b971dd, 0x29b07cd6, 0x038f5fe7, 0x0d8652ec, 0x1f9d45f1, 0x119448fa, 0x4be30393, 0x45ea0e98, 0x57f11985, 0x59f8148e, 0x73c737bf, 0x7dce3ab4, 0x6fd52da9, 0x61dc20a2, 0xad766df6, 0xa37f60fd, 0xb16477e0, 0xbf6d7aeb, 0x955259da, 0x9b5b54d1, 0x894043cc, 0x87494ec7, 0xdd3e05ae, 0xd33708a5, 0xc12c1fb8, 0xcf2512b3, 0xe51a3182, 0xeb133c89, 0xf9082b94, 0xf701269f, 0x4de6bd46, 0x43efb04d, 0x51f4a750, 0x5ffdaa5b, 0x75c2896a, 0x7bcb8461, 0x69d0937c, 0x67d99e77, 0x3daed51e, 0x33a7d815, 0x21bccf08, 0x2fb5c203, 0x058ae132, 0x0b83ec39, 0x1998fb24, 0x1791f62f, 0x764dd68d, 0x7844db86, 0x6a5fcc9b, 0x6456c190, 0x4e69e2a1, 0x4060efaa, 0x527bf8b7, 0x5c72f5bc, 0x0605bed5, 0x080cb3de, 0x1a17a4c3, 0x141ea9c8, 0x3e218af9, 0x302887f2, 0x223390ef, 0x2c3a9de4, 0x96dd063d, 0x98d40b36, 0x8acf1c2b, 0x84c61120, 0xaef93211, 0xa0f03f1a, 0xb2eb2807, 0xbce2250c, 0xe6956e65, 0xe89c636e, 0xfa877473, 0xf48e7978, 0xdeb15a49, 0xd0b85742, 0xc2a3405f, 0xccaa4d54, 0x41ecdaf7, 0x4fe5d7fc, 0x5dfec0e1, 0x53f7cdea, 0x79c8eedb, 0x77c1e3d0, 0x65daf4cd, 0x6bd3f9c6, 0x31a4b2af, 0x3fadbfa4, 0x2db6a8b9, 0x23bfa5b2, 0x09808683, 0x07898b88, 0x15929c95, 0x1b9b919e, 0xa17c0a47, 0xaf75074c, 0xbd6e1051, 0xb3671d5a, 0x99583e6b, 0x97513360, 0x854a247d, 0x8b432976, 0xd134621f, 0xdf3d6f14, 0xcd267809, 0xc32f7502, 0xe9105633, 0xe7195b38, 0xf5024c25, 0xfb0b412e, 0x9ad7618c, 0x94de6c87, 0x86c57b9a, 0x88cc7691, 0xa2f355a0, 0xacfa58ab, 0xbee14fb6, 0xb0e842bd, 0xea9f09d4, 0xe49604df, 0xf68d13c2, 0xf8841ec9, 0xd2bb3df8, 0xdcb230f3, 0xcea927ee, 0xc0a02ae5, 0x7a47b13c, 0x744ebc37, 0x6655ab2a, 0x685ca621, 0x42638510, 0x4c6a881b, 0x5e719f06, 0x5078920d, 0x0a0fd964, 0x0406d46f, 0x161dc372, 0x1814ce79, 0x322bed48, 0x3c22e043, 0x2e39f75e, 0x2030fa55, 0xec9ab701, 0xe293ba0a, 0xf088ad17, 0xfe81a01c, 0xd4be832d, 0xdab78e26, 0xc8ac993b, 0xc6a59430, 0x9cd2df59, 0x92dbd252, 0x80c0c54f, 0x8ec9c844, 0xa4f6eb75, 0xaaffe67e, 0xb8e4f163, 0xb6edfc68, 0x0c0a67b1, 0x02036aba, 0x10187da7, 0x1e1170ac, 0x342e539d, 0x3a275e96, 0x283c498b, 0x26354480, 0x7c420fe9, 0x724b02e2, 0x605015ff, 0x6e5918f4, 0x44663bc5, 0x4a6f36ce, 0x587421d3, 0x567d2cd8, 0x37a10c7a, 0x39a80171, 0x2bb3166c, 0x25ba1b67, 0x0f853856, 0x018c355d, 0x13972240, 0x1d9e2f4b, 0x47e96422, 0x49e06929, 0x5bfb7e34, 0x55f2733f, 0x7fcd500e, 0x71c45d05, 0x63df4a18, 0x6dd64713, 0xd731dcca, 0xd938d1c1, 0xcb23c6dc, 0xc52acbd7, 0xef15e8e6, 0xe11ce5ed, 0xf307f2f0, 0xfd0efffb, 0xa779b492, 0xa970b999, 0xbb6bae84, 0xb562a38f, 0x9f5d80be, 0x91548db5, 0x834f9aa8, 0x8d4697a3];
    var U2 = [0x00000000, 0x0b0e090d, 0x161c121a, 0x1d121b17, 0x2c382434, 0x27362d39, 0x3a24362e, 0x312a3f23, 0x58704868, 0x537e4165, 0x4e6c5a72, 0x4562537f, 0x74486c5c, 0x7f466551, 0x62547e46, 0x695a774b, 0xb0e090d0, 0xbbee99dd, 0xa6fc82ca, 0xadf28bc7, 0x9cd8b4e4, 0x97d6bde9, 0x8ac4a6fe, 0x81caaff3, 0xe890d8b8, 0xe39ed1b5, 0xfe8ccaa2, 0xf582c3af, 0xc4a8fc8c, 0xcfa6f581, 0xd2b4ee96, 0xd9bae79b, 0x7bdb3bbb, 0x70d532b6, 0x6dc729a1, 0x66c920ac, 0x57e31f8f, 0x5ced1682, 0x41ff0d95, 0x4af10498, 0x23ab73d3, 0x28a57ade, 0x35b761c9, 0x3eb968c4, 0x0f9357e7, 0x049d5eea, 0x198f45fd, 0x12814cf0, 0xcb3bab6b, 0xc035a266, 0xdd27b971, 0xd629b07c, 0xe7038f5f, 0xec0d8652, 0xf11f9d45, 0xfa119448, 0x934be303, 0x9845ea0e, 0x8557f119, 0x8e59f814, 0xbf73c737, 0xb47dce3a, 0xa96fd52d, 0xa261dc20, 0xf6ad766d, 0xfda37f60, 0xe0b16477, 0xebbf6d7a, 0xda955259, 0xd19b5b54, 0xcc894043, 0xc787494e, 0xaedd3e05, 0xa5d33708, 0xb8c12c1f, 0xb3cf2512, 0x82e51a31, 0x89eb133c, 0x94f9082b, 0x9ff70126, 0x464de6bd, 0x4d43efb0, 0x5051f4a7, 0x5b5ffdaa, 0x6a75c289, 0x617bcb84, 0x7c69d093, 0x7767d99e, 0x1e3daed5, 0x1533a7d8, 0x0821bccf, 0x032fb5c2, 0x32058ae1, 0x390b83ec, 0x241998fb, 0x2f1791f6, 0x8d764dd6, 0x867844db, 0x9b6a5fcc, 0x906456c1, 0xa14e69e2, 0xaa4060ef, 0xb7527bf8, 0xbc5c72f5, 0xd50605be, 0xde080cb3, 0xc31a17a4, 0xc8141ea9, 0xf93e218a, 0xf2302887, 0xef223390, 0xe42c3a9d, 0x3d96dd06, 0x3698d40b, 0x2b8acf1c, 0x2084c611, 0x11aef932, 0x1aa0f03f, 0x07b2eb28, 0x0cbce225, 0x65e6956e, 0x6ee89c63, 0x73fa8774, 0x78f48e79, 0x49deb15a, 0x42d0b857, 0x5fc2a340, 0x54ccaa4d, 0xf741ecda, 0xfc4fe5d7, 0xe15dfec0, 0xea53f7cd, 0xdb79c8ee, 0xd077c1e3, 0xcd65daf4, 0xc66bd3f9, 0xaf31a4b2, 0xa43fadbf, 0xb92db6a8, 0xb223bfa5, 0x83098086, 0x8807898b, 0x9515929c, 0x9e1b9b91, 0x47a17c0a, 0x4caf7507, 0x51bd6e10, 0x5ab3671d, 0x6b99583e, 0x60975133, 0x7d854a24, 0x768b4329, 0x1fd13462, 0x14df3d6f, 0x09cd2678, 0x02c32f75, 0x33e91056, 0x38e7195b, 0x25f5024c, 0x2efb0b41, 0x8c9ad761, 0x8794de6c, 0x9a86c57b, 0x9188cc76, 0xa0a2f355, 0xabacfa58, 0xb6bee14f, 0xbdb0e842, 0xd4ea9f09, 0xdfe49604, 0xc2f68d13, 0xc9f8841e, 0xf8d2bb3d, 0xf3dcb230, 0xeecea927, 0xe5c0a02a, 0x3c7a47b1, 0x37744ebc, 0x2a6655ab, 0x21685ca6, 0x10426385, 0x1b4c6a88, 0x065e719f, 0x0d507892, 0x640a0fd9, 0x6f0406d4, 0x72161dc3, 0x791814ce, 0x48322bed, 0x433c22e0, 0x5e2e39f7, 0x552030fa, 0x01ec9ab7, 0x0ae293ba, 0x17f088ad, 0x1cfe81a0, 0x2dd4be83, 0x26dab78e, 0x3bc8ac99, 0x30c6a594, 0x599cd2df, 0x5292dbd2, 0x4f80c0c5, 0x448ec9c8, 0x75a4f6eb, 0x7eaaffe6, 0x63b8e4f1, 0x68b6edfc, 0xb10c0a67, 0xba02036a, 0xa710187d, 0xac1e1170, 0x9d342e53, 0x963a275e, 0x8b283c49, 0x80263544, 0xe97c420f, 0xe2724b02, 0xff605015, 0xf46e5918, 0xc544663b, 0xce4a6f36, 0xd3587421, 0xd8567d2c, 0x7a37a10c, 0x7139a801, 0x6c2bb316, 0x6725ba1b, 0x560f8538, 0x5d018c35, 0x40139722, 0x4b1d9e2f, 0x2247e964, 0x2949e069, 0x345bfb7e, 0x3f55f273, 0x0e7fcd50, 0x0571c45d, 0x1863df4a, 0x136dd647, 0xcad731dc, 0xc1d938d1, 0xdccb23c6, 0xd7c52acb, 0xe6ef15e8, 0xede11ce5, 0xf0f307f2, 0xfbfd0eff, 0x92a779b4, 0x99a970b9, 0x84bb6bae, 0x8fb562a3, 0xbe9f5d80, 0xb591548d, 0xa8834f9a, 0xa38d4697];
    var U3 = [0x00000000, 0x0d0b0e09, 0x1a161c12, 0x171d121b, 0x342c3824, 0x3927362d, 0x2e3a2436, 0x23312a3f, 0x68587048, 0x65537e41, 0x724e6c5a, 0x7f456253, 0x5c74486c, 0x517f4665, 0x4662547e, 0x4b695a77, 0xd0b0e090, 0xddbbee99, 0xcaa6fc82, 0xc7adf28b, 0xe49cd8b4, 0xe997d6bd, 0xfe8ac4a6, 0xf381caaf, 0xb8e890d8, 0xb5e39ed1, 0xa2fe8cca, 0xaff582c3, 0x8cc4a8fc, 0x81cfa6f5, 0x96d2b4ee, 0x9bd9bae7, 0xbb7bdb3b, 0xb670d532, 0xa16dc729, 0xac66c920, 0x8f57e31f, 0x825ced16, 0x9541ff0d, 0x984af104, 0xd323ab73, 0xde28a57a, 0xc935b761, 0xc43eb968, 0xe70f9357, 0xea049d5e, 0xfd198f45, 0xf012814c, 0x6bcb3bab, 0x66c035a2, 0x71dd27b9, 0x7cd629b0, 0x5fe7038f, 0x52ec0d86, 0x45f11f9d, 0x48fa1194, 0x03934be3, 0x0e9845ea, 0x198557f1, 0x148e59f8, 0x37bf73c7, 0x3ab47dce, 0x2da96fd5, 0x20a261dc, 0x6df6ad76, 0x60fda37f, 0x77e0b164, 0x7aebbf6d, 0x59da9552, 0x54d19b5b, 0x43cc8940, 0x4ec78749, 0x05aedd3e, 0x08a5d337, 0x1fb8c12c, 0x12b3cf25, 0x3182e51a, 0x3c89eb13, 0x2b94f908, 0x269ff701, 0xbd464de6, 0xb04d43ef, 0xa75051f4, 0xaa5b5ffd, 0x896a75c2, 0x84617bcb, 0x937c69d0, 0x9e7767d9, 0xd51e3dae, 0xd81533a7, 0xcf0821bc, 0xc2032fb5, 0xe132058a, 0xec390b83, 0xfb241998, 0xf62f1791, 0xd68d764d, 0xdb867844, 0xcc9b6a5f, 0xc1906456, 0xe2a14e69, 0xefaa4060, 0xf8b7527b, 0xf5bc5c72, 0xbed50605, 0xb3de080c, 0xa4c31a17, 0xa9c8141e, 0x8af93e21, 0x87f23028, 0x90ef2233, 0x9de42c3a, 0x063d96dd, 0x0b3698d4, 0x1c2b8acf, 0x112084c6, 0x3211aef9, 0x3f1aa0f0, 0x2807b2eb, 0x250cbce2, 0x6e65e695, 0x636ee89c, 0x7473fa87, 0x7978f48e, 0x5a49deb1, 0x5742d0b8, 0x405fc2a3, 0x4d54ccaa, 0xdaf741ec, 0xd7fc4fe5, 0xc0e15dfe, 0xcdea53f7, 0xeedb79c8, 0xe3d077c1, 0xf4cd65da, 0xf9c66bd3, 0xb2af31a4, 0xbfa43fad, 0xa8b92db6, 0xa5b223bf, 0x86830980, 0x8b880789, 0x9c951592, 0x919e1b9b, 0x0a47a17c, 0x074caf75, 0x1051bd6e, 0x1d5ab367, 0x3e6b9958, 0x33609751, 0x247d854a, 0x29768b43, 0x621fd134, 0x6f14df3d, 0x7809cd26, 0x7502c32f, 0x5633e910, 0x5b38e719, 0x4c25f502, 0x412efb0b, 0x618c9ad7, 0x6c8794de, 0x7b9a86c5, 0x769188cc, 0x55a0a2f3, 0x58abacfa, 0x4fb6bee1, 0x42bdb0e8, 0x09d4ea9f, 0x04dfe496, 0x13c2f68d, 0x1ec9f884, 0x3df8d2bb, 0x30f3dcb2, 0x27eecea9, 0x2ae5c0a0, 0xb13c7a47, 0xbc37744e, 0xab2a6655, 0xa621685c, 0x85104263, 0x881b4c6a, 0x9f065e71, 0x920d5078, 0xd9640a0f, 0xd46f0406, 0xc372161d, 0xce791814, 0xed48322b, 0xe0433c22, 0xf75e2e39, 0xfa552030, 0xb701ec9a, 0xba0ae293, 0xad17f088, 0xa01cfe81, 0x832dd4be, 0x8e26dab7, 0x993bc8ac, 0x9430c6a5, 0xdf599cd2, 0xd25292db, 0xc54f80c0, 0xc8448ec9, 0xeb75a4f6, 0xe67eaaff, 0xf163b8e4, 0xfc68b6ed, 0x67b10c0a, 0x6aba0203, 0x7da71018, 0x70ac1e11, 0x539d342e, 0x5e963a27, 0x498b283c, 0x44802635, 0x0fe97c42, 0x02e2724b, 0x15ff6050, 0x18f46e59, 0x3bc54466, 0x36ce4a6f, 0x21d35874, 0x2cd8567d, 0x0c7a37a1, 0x017139a8, 0x166c2bb3, 0x1b6725ba, 0x38560f85, 0x355d018c, 0x22401397, 0x2f4b1d9e, 0x642247e9, 0x692949e0, 0x7e345bfb, 0x733f55f2, 0x500e7fcd, 0x5d0571c4, 0x4a1863df, 0x47136dd6, 0xdccad731, 0xd1c1d938, 0xc6dccb23, 0xcbd7c52a, 0xe8e6ef15, 0xe5ede11c, 0xf2f0f307, 0xfffbfd0e, 0xb492a779, 0xb999a970, 0xae84bb6b, 0xa38fb562, 0x80be9f5d, 0x8db59154, 0x9aa8834f, 0x97a38d46];
    var U4 = [0x00000000, 0x090d0b0e, 0x121a161c, 0x1b171d12, 0x24342c38, 0x2d392736, 0x362e3a24, 0x3f23312a, 0x48685870, 0x4165537e, 0x5a724e6c, 0x537f4562, 0x6c5c7448, 0x65517f46, 0x7e466254, 0x774b695a, 0x90d0b0e0, 0x99ddbbee, 0x82caa6fc, 0x8bc7adf2, 0xb4e49cd8, 0xbde997d6, 0xa6fe8ac4, 0xaff381ca, 0xd8b8e890, 0xd1b5e39e, 0xcaa2fe8c, 0xc3aff582, 0xfc8cc4a8, 0xf581cfa6, 0xee96d2b4, 0xe79bd9ba, 0x3bbb7bdb, 0x32b670d5, 0x29a16dc7, 0x20ac66c9, 0x1f8f57e3, 0x16825ced, 0x0d9541ff, 0x04984af1, 0x73d323ab, 0x7ade28a5, 0x61c935b7, 0x68c43eb9, 0x57e70f93, 0x5eea049d, 0x45fd198f, 0x4cf01281, 0xab6bcb3b, 0xa266c035, 0xb971dd27, 0xb07cd629, 0x8f5fe703, 0x8652ec0d, 0x9d45f11f, 0x9448fa11, 0xe303934b, 0xea0e9845, 0xf1198557, 0xf8148e59, 0xc737bf73, 0xce3ab47d, 0xd52da96f, 0xdc20a261, 0x766df6ad, 0x7f60fda3, 0x6477e0b1, 0x6d7aebbf, 0x5259da95, 0x5b54d19b, 0x4043cc89, 0x494ec787, 0x3e05aedd, 0x3708a5d3, 0x2c1fb8c1, 0x2512b3cf, 0x1a3182e5, 0x133c89eb, 0x082b94f9, 0x01269ff7, 0xe6bd464d, 0xefb04d43, 0xf4a75051, 0xfdaa5b5f, 0xc2896a75, 0xcb84617b, 0xd0937c69, 0xd99e7767, 0xaed51e3d, 0xa7d81533, 0xbccf0821, 0xb5c2032f, 0x8ae13205, 0x83ec390b, 0x98fb2419, 0x91f62f17, 0x4dd68d76, 0x44db8678, 0x5fcc9b6a, 0x56c19064, 0x69e2a14e, 0x60efaa40, 0x7bf8b752, 0x72f5bc5c, 0x05bed506, 0x0cb3de08, 0x17a4c31a, 0x1ea9c814, 0x218af93e, 0x2887f230, 0x3390ef22, 0x3a9de42c, 0xdd063d96, 0xd40b3698, 0xcf1c2b8a, 0xc6112084, 0xf93211ae, 0xf03f1aa0, 0xeb2807b2, 0xe2250cbc, 0x956e65e6, 0x9c636ee8, 0x877473fa, 0x8e7978f4, 0xb15a49de, 0xb85742d0, 0xa3405fc2, 0xaa4d54cc, 0xecdaf741, 0xe5d7fc4f, 0xfec0e15d, 0xf7cdea53, 0xc8eedb79, 0xc1e3d077, 0xdaf4cd65, 0xd3f9c66b, 0xa4b2af31, 0xadbfa43f, 0xb6a8b92d, 0xbfa5b223, 0x80868309, 0x898b8807, 0x929c9515, 0x9b919e1b, 0x7c0a47a1, 0x75074caf, 0x6e1051bd, 0x671d5ab3, 0x583e6b99, 0x51336097, 0x4a247d85, 0x4329768b, 0x34621fd1, 0x3d6f14df, 0x267809cd, 0x2f7502c3, 0x105633e9, 0x195b38e7, 0x024c25f5, 0x0b412efb, 0xd7618c9a, 0xde6c8794, 0xc57b9a86, 0xcc769188, 0xf355a0a2, 0xfa58abac, 0xe14fb6be, 0xe842bdb0, 0x9f09d4ea, 0x9604dfe4, 0x8d13c2f6, 0x841ec9f8, 0xbb3df8d2, 0xb230f3dc, 0xa927eece, 0xa02ae5c0, 0x47b13c7a, 0x4ebc3774, 0x55ab2a66, 0x5ca62168, 0x63851042, 0x6a881b4c, 0x719f065e, 0x78920d50, 0x0fd9640a, 0x06d46f04, 0x1dc37216, 0x14ce7918, 0x2bed4832, 0x22e0433c, 0x39f75e2e, 0x30fa5520, 0x9ab701ec, 0x93ba0ae2, 0x88ad17f0, 0x81a01cfe, 0xbe832dd4, 0xb78e26da, 0xac993bc8, 0xa59430c6, 0xd2df599c, 0xdbd25292, 0xc0c54f80, 0xc9c8448e, 0xf6eb75a4, 0xffe67eaa, 0xe4f163b8, 0xedfc68b6, 0x0a67b10c, 0x036aba02, 0x187da710, 0x1170ac1e, 0x2e539d34, 0x275e963a, 0x3c498b28, 0x35448026, 0x420fe97c, 0x4b02e272, 0x5015ff60, 0x5918f46e, 0x663bc544, 0x6f36ce4a, 0x7421d358, 0x7d2cd856, 0xa10c7a37, 0xa8017139, 0xb3166c2b, 0xba1b6725, 0x8538560f, 0x8c355d01, 0x97224013, 0x9e2f4b1d, 0xe9642247, 0xe0692949, 0xfb7e345b, 0xf2733f55, 0xcd500e7f, 0xc45d0571, 0xdf4a1863, 0xd647136d, 0x31dccad7, 0x38d1c1d9, 0x23c6dccb, 0x2acbd7c5, 0x15e8e6ef, 0x1ce5ede1, 0x07f2f0f3, 0x0efffbfd, 0x79b492a7, 0x70b999a9, 0x6bae84bb, 0x62a38fb5, 0x5d80be9f, 0x548db591, 0x4f9aa883, 0x4697a38d];

    function convertToInt32(bytes) {
        var result = [];
        for (var i = 0; i < bytes.length; i += 4) {
            result.push(
                (bytes[i] << 24) |
                (bytes[i + 1] << 16) |
                (bytes[i + 2] << 8) |
                bytes[i + 3]
            );
        }
        return result;
    }

    var AES = function (key) {
        if (!(this instanceof AES)) {
            throw Error('AES must be instanitated with `new`');
        }

        Object.defineProperty(this, 'key', {
            value: coerceArray(key, true)
        });

        this._prepare();
    }


    AES.prototype._prepare = function () {

        var rounds = numberOfRounds[this.key.length];
        if (rounds == null) {
            throw new Error('invalid key size (must be 16, 24 or 32 bytes)');
        }

        // encryption round keys
        this._Ke = [];

        // decryption round keys
        this._Kd = [];

        for (var i = 0; i <= rounds; i++) {
            this._Ke.push([0, 0, 0, 0]);
            this._Kd.push([0, 0, 0, 0]);
        }

        var roundKeyCount = (rounds + 1) * 4;
        var KC = this.key.length / 4;

        // convert the key into ints
        var tk = convertToInt32(this.key);

        // copy values into round key arrays
        var index;
        for (var i = 0; i < KC; i++) {
            index = i >> 2;
            this._Ke[index][i % 4] = tk[i];
            this._Kd[rounds - index][i % 4] = tk[i];
        }

        // key expansion (fips-197 section 5.2)
        var rconpointer = 0;
        var t = KC, tt;
        while (t < roundKeyCount) {
            tt = tk[KC - 1];
            tk[0] ^= ((S[(tt >> 16) & 0xFF] << 24) ^
                (S[(tt >> 8) & 0xFF] << 16) ^
                (S[tt & 0xFF] << 8) ^
                S[(tt >> 24) & 0xFF] ^
                (rcon[rconpointer] << 24));
            rconpointer += 1;

            // key expansion (for non-256 bit)
            if (KC != 8) {
                for (var i = 1; i < KC; i++) {
                    tk[i] ^= tk[i - 1];
                }

                // key expansion for 256-bit keys is "slightly different" (fips-197)
            } else {
                for (var i = 1; i < (KC / 2); i++) {
                    tk[i] ^= tk[i - 1];
                }
                tt = tk[(KC / 2) - 1];

                tk[KC / 2] ^= (S[tt & 0xFF] ^
                    (S[(tt >> 8) & 0xFF] << 8) ^
                    (S[(tt >> 16) & 0xFF] << 16) ^
                    (S[(tt >> 24) & 0xFF] << 24));

                for (var i = (KC / 2) + 1; i < KC; i++) {
                    tk[i] ^= tk[i - 1];
                }
            }

            // copy values into round key arrays
            var i = 0, r, c;
            while (i < KC && t < roundKeyCount) {
                r = t >> 2;
                c = t % 4;
                this._Ke[r][c] = tk[i];
                this._Kd[rounds - r][c] = tk[i++];
                t++;
            }
        }

        // inverse-cipher-ify the decryption round key (fips-197 section 5.3)
        for (var r = 1; r < rounds; r++) {
            for (var c = 0; c < 4; c++) {
                tt = this._Kd[r][c];
                this._Kd[r][c] = (U1[(tt >> 24) & 0xFF] ^
                    U2[(tt >> 16) & 0xFF] ^
                    U3[(tt >> 8) & 0xFF] ^
                    U4[tt & 0xFF]);
            }
        }
    }

    AES.prototype.encrypt = function (plaintext) {
        if (plaintext.length != 16) {
            throw new Error('invalid plaintext size (must be 16 bytes)');
        }

        var rounds = this._Ke.length - 1;
        var a = [0, 0, 0, 0];

        // convert plaintext to (ints ^ key)
        var t = convertToInt32(plaintext);
        for (var i = 0; i < 4; i++) {
            t[i] ^= this._Ke[0][i];
        }

        // apply round transforms
        for (var r = 1; r < rounds; r++) {
            for (var i = 0; i < 4; i++) {
                a[i] = (T1[(t[i] >> 24) & 0xff] ^
                    T2[(t[(i + 1) % 4] >> 16) & 0xff] ^
                    T3[(t[(i + 2) % 4] >> 8) & 0xff] ^
                    T4[t[(i + 3) % 4] & 0xff] ^
                    this._Ke[r][i]);
            }
            t = a.slice();
        }

        // the last round is special
        var result = createArray(16), tt;
        for (var i = 0; i < 4; i++) {
            tt = this._Ke[rounds][i];
            result[4 * i] = (S[(t[i] >> 24) & 0xff] ^ (tt >> 24)) & 0xff;
            result[4 * i + 1] = (S[(t[(i + 1) % 4] >> 16) & 0xff] ^ (tt >> 16)) & 0xff;
            result[4 * i + 2] = (S[(t[(i + 2) % 4] >> 8) & 0xff] ^ (tt >> 8)) & 0xff;
            result[4 * i + 3] = (S[t[(i + 3) % 4] & 0xff] ^ tt) & 0xff;
        }

        return result;
    }

    AES.prototype.decrypt = function (ciphertext) {
        if (ciphertext.length != 16) {
            throw new Error('invalid ciphertext size (must be 16 bytes)');
        }

        var rounds = this._Kd.length - 1;
        var a = [0, 0, 0, 0];

        // convert plaintext to (ints ^ key)
        var t = convertToInt32(ciphertext);
        for (var i = 0; i < 4; i++) {
            t[i] ^= this._Kd[0][i];
        }

        // apply round transforms
        for (var r = 1; r < rounds; r++) {
            for (var i = 0; i < 4; i++) {
                a[i] = (T5[(t[i] >> 24) & 0xff] ^
                    T6[(t[(i + 3) % 4] >> 16) & 0xff] ^
                    T7[(t[(i + 2) % 4] >> 8) & 0xff] ^
                    T8[t[(i + 1) % 4] & 0xff] ^
                    this._Kd[r][i]);
            }
            t = a.slice();
        }

        // the last round is special
        var result = createArray(16), tt;
        for (var i = 0; i < 4; i++) {
            tt = this._Kd[rounds][i];
            result[4 * i] = (Si[(t[i] >> 24) & 0xff] ^ (tt >> 24)) & 0xff;
            result[4 * i + 1] = (Si[(t[(i + 3) % 4] >> 16) & 0xff] ^ (tt >> 16)) & 0xff;
            result[4 * i + 2] = (Si[(t[(i + 2) % 4] >> 8) & 0xff] ^ (tt >> 8)) & 0xff;
            result[4 * i + 3] = (Si[t[(i + 1) % 4] & 0xff] ^ tt) & 0xff;
        }

        return result;
    }


    /**
     *  Mode Of Operation - Electonic Codebook (ECB)
     */
    var ModeOfOperationECB = function (key) {
        if (!(this instanceof ModeOfOperationECB)) {
            throw Error('AES must be instanitated with `new`');
        }

        this.description = "Electronic Code Block";
        this.name = "ecb";

        this._aes = new AES(key);
    }

    ModeOfOperationECB.prototype.encrypt = function (plaintext) {
        plaintext = coerceArray(plaintext);

        if ((plaintext.length % 16) !== 0) {
            throw new Error('invalid plaintext size (must be multiple of 16 bytes)');
        }

        var ciphertext = createArray(plaintext.length);
        var block = createArray(16);

        for (var i = 0; i < plaintext.length; i += 16) {
            copyArray(plaintext, block, 0, i, i + 16);
            block = this._aes.encrypt(block);
            copyArray(block, ciphertext, i);
        }

        return ciphertext;
    }

    ModeOfOperationECB.prototype.decrypt = function (ciphertext) {
        ciphertext = coerceArray(ciphertext);

        if ((ciphertext.length % 16) !== 0) {
            throw new Error('invalid ciphertext size (must be multiple of 16 bytes)');
        }

        var plaintext = createArray(ciphertext.length);
        var block = createArray(16);

        for (var i = 0; i < ciphertext.length; i += 16) {
            copyArray(ciphertext, block, 0, i, i + 16);
            block = this._aes.decrypt(block);
            copyArray(block, plaintext, i);
        }

        return plaintext;
    }


    /**
     *  Mode Of Operation - Cipher Block Chaining (CBC)
     */
    var ModeOfOperationCBC = function (key, iv) {
        if (!(this instanceof ModeOfOperationCBC)) {
            throw Error('AES must be instanitated with `new`');
        }

        this.description = "Cipher Block Chaining";
        this.name = "cbc";

        if (!iv) {
            iv = createArray(16);

        } else if (iv.length != 16) {
            throw new Error('invalid initialation vector size (must be 16 bytes)');
        }

        this._lastCipherblock = coerceArray(iv, true);

        this._aes = new AES(key);
    }

    ModeOfOperationCBC.prototype.encrypt = function (plaintext) {
        plaintext = coerceArray(plaintext);

        if ((plaintext.length % 16) !== 0) {
            throw new Error('invalid plaintext size (must be multiple of 16 bytes)');
        }

        var ciphertext = createArray(plaintext.length);
        var block = createArray(16);

        for (var i = 0; i < plaintext.length; i += 16) {
            copyArray(plaintext, block, 0, i, i + 16);

            for (var j = 0; j < 16; j++) {
                block[j] ^= this._lastCipherblock[j];
            }

            this._lastCipherblock = this._aes.encrypt(block);
            copyArray(this._lastCipherblock, ciphertext, i);
        }

        return ciphertext;
    }

    ModeOfOperationCBC.prototype.decrypt = function (ciphertext) {
        ciphertext = coerceArray(ciphertext);

        if ((ciphertext.length % 16) !== 0) {
            throw new Error('invalid ciphertext size (must be multiple of 16 bytes)');
        }

        var plaintext = createArray(ciphertext.length);
        var block = createArray(16);

        for (var i = 0; i < ciphertext.length; i += 16) {
            copyArray(ciphertext, block, 0, i, i + 16);
            block = this._aes.decrypt(block);

            for (var j = 0; j < 16; j++) {
                plaintext[i + j] = block[j] ^ this._lastCipherblock[j];
            }

            copyArray(ciphertext, this._lastCipherblock, 0, i, i + 16);
        }

        return plaintext;
    }


    /**
     *  Mode Of Operation - Cipher Feedback (CFB)
     */
    var ModeOfOperationCFB = function (key, iv, segmentSize) {
        if (!(this instanceof ModeOfOperationCFB)) {
            throw Error('AES must be instanitated with `new`');
        }

        this.description = "Cipher Feedback";
        this.name = "cfb";

        if (!iv) {
            iv = createArray(16);

        } else if (iv.length != 16) {
            throw new Error('invalid initialation vector size (must be 16 size)');
        }

        if (!segmentSize) { segmentSize = 1; }

        this.segmentSize = segmentSize;

        this._shiftRegister = coerceArray(iv, true);

        this._aes = new AES(key);
    }

    ModeOfOperationCFB.prototype.encrypt = function (plaintext) {
        if ((plaintext.length % this.segmentSize) != 0) {
            throw new Error('invalid plaintext size (must be segmentSize bytes)');
        }

        var encrypted = coerceArray(plaintext, true);

        var xorSegment;
        for (var i = 0; i < encrypted.length; i += this.segmentSize) {
            xorSegment = this._aes.encrypt(this._shiftRegister);
            for (var j = 0; j < this.segmentSize; j++) {
                encrypted[i + j] ^= xorSegment[j];
            }

            // Shift the register
            copyArray(this._shiftRegister, this._shiftRegister, 0, this.segmentSize);
            copyArray(encrypted, this._shiftRegister, 16 - this.segmentSize, i, i + this.segmentSize);
        }

        return encrypted;
    }

    ModeOfOperationCFB.prototype.decrypt = function (ciphertext) {
        if ((ciphertext.length % this.segmentSize) != 0) {
            throw new Error('invalid ciphertext size (must be segmentSize bytes)');
        }

        var plaintext = coerceArray(ciphertext, true);

        var xorSegment;
        for (var i = 0; i < plaintext.length; i += this.segmentSize) {
            xorSegment = this._aes.encrypt(this._shiftRegister);

            for (var j = 0; j < this.segmentSize; j++) {
                plaintext[i + j] ^= xorSegment[j];
            }

            // Shift the register
            copyArray(this._shiftRegister, this._shiftRegister, 0, this.segmentSize);
            copyArray(ciphertext, this._shiftRegister, 16 - this.segmentSize, i, i + this.segmentSize);
        }

        return plaintext;
    }

    /**
     *  Mode Of Operation - Output Feedback (OFB)
     */
    var ModeOfOperationOFB = function (key, iv) {
        if (!(this instanceof ModeOfOperationOFB)) {
            throw Error('AES must be instanitated with `new`');
        }

        this.description = "Output Feedback";
        this.name = "ofb";

        if (!iv) {
            iv = createArray(16);

        } else if (iv.length != 16) {
            throw new Error('invalid initialation vector size (must be 16 bytes)');
        }

        this._lastPrecipher = coerceArray(iv, true);
        this._lastPrecipherIndex = 16;

        this._aes = new AES(key);
    }

    ModeOfOperationOFB.prototype.encrypt = function (plaintext) {
        var encrypted = coerceArray(plaintext, true);

        for (var i = 0; i < encrypted.length; i++) {
            if (this._lastPrecipherIndex === 16) {
                this._lastPrecipher = this._aes.encrypt(this._lastPrecipher);
                this._lastPrecipherIndex = 0;
            }
            encrypted[i] ^= this._lastPrecipher[this._lastPrecipherIndex++];
        }

        return encrypted;
    }

    // Decryption is symetric
    ModeOfOperationOFB.prototype.decrypt = ModeOfOperationOFB.prototype.encrypt;


    /**
     *  Counter object for CTR common mode of operation
     */
    var Counter = function (initialValue) {
        if (!(this instanceof Counter)) {
            throw Error('Counter must be instanitated with `new`');
        }

        // We allow 0, but anything false-ish uses the default 1
        if (initialValue !== 0 && !initialValue) { initialValue = 1; }

        if (typeof (initialValue) === 'number') {
            this._counter = createArray(16);
            this.setValue(initialValue);

        } else {
            this.setBytes(initialValue);
        }
    }

    Counter.prototype.setValue = function (value) {
        if (typeof (value) !== 'number' || parseInt(value) != value) {
            throw new Error('invalid counter value (must be an integer)');
        }

        // We cannot safely handle numbers beyond the safe range for integers
        if (value > Number.MAX_SAFE_INTEGER) {
            throw new Error('integer value out of safe range');
        }

        for (var index = 15; index >= 0; --index) {
            this._counter[index] = value % 256;
            value = parseInt(value / 256);
        }
    }

    Counter.prototype.setBytes = function (bytes) {
        bytes = coerceArray(bytes, true);

        if (bytes.length != 16) {
            throw new Error('invalid counter bytes size (must be 16 bytes)');
        }

        this._counter = bytes;
    };

    Counter.prototype.increment = function () {
        for (var i = 15; i >= 0; i--) {
            if (this._counter[i] === 255) {
                this._counter[i] = 0;
            } else {
                this._counter[i]++;
                break;
            }
        }
    }


    /**
     *  Mode Of Operation - Counter (CTR)
     */
    var ModeOfOperationCTR = function (key, counter) {
        if (!(this instanceof ModeOfOperationCTR)) {
            throw Error('AES must be instanitated with `new`');
        }

        this.description = "Counter";
        this.name = "ctr";

        if (!(counter instanceof Counter)) {
            counter = new Counter(counter)
        }

        this._counter = counter;

        this._remainingCounter = null;
        this._remainingCounterIndex = 16;

        this._aes = new AES(key);
    }

    ModeOfOperationCTR.prototype.encrypt = function (plaintext) {
        var encrypted = coerceArray(plaintext, true);

        for (var i = 0; i < encrypted.length; i++) {
            if (this._remainingCounterIndex === 16) {
                this._remainingCounter = this._aes.encrypt(this._counter._counter);
                this._remainingCounterIndex = 0;
                this._counter.increment();
            }
            encrypted[i] ^= this._remainingCounter[this._remainingCounterIndex++];
        }

        return encrypted;
    }

    // Decryption is symetric
    ModeOfOperationCTR.prototype.decrypt = ModeOfOperationCTR.prototype.encrypt;


    ///////////////////////
    // Padding

    // See:https://tools.ietf.org/html/rfc2315
    function pkcs7pad(data) {
        data = coerceArray(data, true);
        var padder = 16 - (data.length % 16);
        var result = createArray(data.length + padder);
        copyArray(data, result);
        for (var i = data.length; i < result.length; i++) {
            result[i] = padder;
        }
        return result;
    }

    function pkcs7strip(data) {
        data = coerceArray(data, true);
        if (data.length < 16) { throw new Error('PKCS#7 invalid length'); }

        var padder = data[data.length - 1];
        if (padder > 16) { throw new Error('PKCS#7 padding byte out of range'); }

        var length = data.length - padder;
        for (var i = 0; i < padder; i++) {
            if (data[length + i] !== padder) {
                throw new Error('PKCS#7 invalid padding byte');
            }
        }

        var result = createArray(length);
        copyArray(data, result, 0, 0, length);
        return result;
    }

    ///////////////////////
    // Exporting


    // The block cipher
    var aesjs = {
        AES: AES,
        Counter: Counter,

        ModeOfOperation: {
            ecb: ModeOfOperationECB,
            cbc: ModeOfOperationCBC,
            cfb: ModeOfOperationCFB,
            ofb: ModeOfOperationOFB,
            ctr: ModeOfOperationCTR
        },

        utils: {
            hex: convertHex,
            utf8: convertUtf8
        },

        padding: {
            pkcs7: {
                pad: pkcs7pad,
                strip: pkcs7strip
            }
        },

        _arrayTest: {
            coerceArray: coerceArray,
            createArray: createArray,
            copyArray: copyArray,
        }
    };


    // node.js
    if (typeof exports !== 'undefined') {
        module.exports = aesjs

        // RequireJS/AMD
        // http://www.requirejs.org/docs/api.html
        // https://github.com/amdjs/amdjs-api/wiki/AMD
    } else if (typeof (define) === 'function' && define.amd) {
        define([], function () { return aesjs; });

        // Web Browsers
    } else {

        // If there was an existing library at "aesjs" make sure it's still available
        if (root.aesjs) {
            aesjs._aesjs = root.aesjs;
        }

        root.aesjs = aesjs;
    }


})(this);



/* pako 1.0.3 nodeca/pako
 https://cdn.jsdelivr.net/pako/1.0.3/pako.min.js
 https://github.com/nodeca/pako
 */
!function (t) { if ("object" == typeof exports && "undefined" != typeof module) module.exports = t(); else if ("function" == typeof define && define.amd) define([], t); else { var e; e = "undefined" != typeof window ? window : "undefined" != typeof global ? global : "undefined" != typeof self ? self : this, e.pako = t() } }(function () {
    return function t(e, a, i) { function n(s, o) { if (!a[s]) { if (!e[s]) { var l = "function" == typeof require && require; if (!o && l) return l(s, !0); if (r) return r(s, !0); var h = new Error("Cannot find module '" + s + "'"); throw h.code = "MODULE_NOT_FOUND", h } var d = a[s] = { exports: {} }; e[s][0].call(d.exports, function (t) { var a = e[s][1][t]; return n(a ? a : t) }, d, d.exports, t, e, a, i) } return a[s].exports } for (var r = "function" == typeof require && require, s = 0; s < i.length; s++)n(i[s]); return n }({
        1: [function (t, e, a) { "use strict"; function i(t) { if (!(this instanceof i)) return new i(t); this.options = l.assign({ level: w, method: v, chunkSize: 16384, windowBits: 15, memLevel: 8, strategy: p, to: "" }, t || {}); var e = this.options; e.raw && e.windowBits > 0 ? e.windowBits = -e.windowBits : e.gzip && e.windowBits > 0 && e.windowBits < 16 && (e.windowBits += 16), this.err = 0, this.msg = "", this.ended = !1, this.chunks = [], this.strm = new f, this.strm.avail_out = 0; var a = o.deflateInit2(this.strm, e.level, e.method, e.windowBits, e.memLevel, e.strategy); if (a !== b) throw new Error(d[a]); if (e.header && o.deflateSetHeader(this.strm, e.header), e.dictionary) { var n; if (n = "string" == typeof e.dictionary ? h.string2buf(e.dictionary) : "[object ArrayBuffer]" === _.call(e.dictionary) ? new Uint8Array(e.dictionary) : e.dictionary, a = o.deflateSetDictionary(this.strm, n), a !== b) throw new Error(d[a]); this._dict_set = !0 } } function n(t, e) { var a = new i(e); if (a.push(t, !0), a.err) throw a.msg; return a.result } function r(t, e) { return e = e || {}, e.raw = !0, n(t, e) } function s(t, e) { return e = e || {}, e.gzip = !0, n(t, e) } var o = t("./zlib/deflate"), l = t("./utils/common"), h = t("./utils/strings"), d = t("./zlib/messages"), f = t("./zlib/zstream"), _ = Object.prototype.toString, u = 0, c = 4, b = 0, g = 1, m = 2, w = -1, p = 0, v = 8; i.prototype.push = function (t, e) { var a, i, n = this.strm, r = this.options.chunkSize; if (this.ended) return !1; i = e === ~~e ? e : e === !0 ? c : u, "string" == typeof t ? n.input = h.string2buf(t) : "[object ArrayBuffer]" === _.call(t) ? n.input = new Uint8Array(t) : n.input = t, n.next_in = 0, n.avail_in = n.input.length; do { if (0 === n.avail_out && (n.output = new l.Buf8(r), n.next_out = 0, n.avail_out = r), a = o.deflate(n, i), a !== g && a !== b) return this.onEnd(a), this.ended = !0, !1; 0 !== n.avail_out && (0 !== n.avail_in || i !== c && i !== m) || ("string" === this.options.to ? this.onData(h.buf2binstring(l.shrinkBuf(n.output, n.next_out))) : this.onData(l.shrinkBuf(n.output, n.next_out))) } while ((n.avail_in > 0 || 0 === n.avail_out) && a !== g); return i === c ? (a = o.deflateEnd(this.strm), this.onEnd(a), this.ended = !0, a === b) : i !== m || (this.onEnd(b), n.avail_out = 0, !0) }, i.prototype.onData = function (t) { this.chunks.push(t) }, i.prototype.onEnd = function (t) { t === b && ("string" === this.options.to ? this.result = this.chunks.join("") : this.result = l.flattenChunks(this.chunks)), this.chunks = [], this.err = t, this.msg = this.strm.msg }, a.Deflate = i, a.deflate = n, a.deflateRaw = r, a.gzip = s }, { "./utils/common": 3, "./utils/strings": 4, "./zlib/deflate": 8, "./zlib/messages": 13, "./zlib/zstream": 15 }], 2: [function (t, e, a) { "use strict"; function i(t) { if (!(this instanceof i)) return new i(t); this.options = o.assign({ chunkSize: 16384, windowBits: 0, to: "" }, t || {}); var e = this.options; e.raw && e.windowBits >= 0 && e.windowBits < 16 && (e.windowBits = -e.windowBits, 0 === e.windowBits && (e.windowBits = -15)), !(e.windowBits >= 0 && e.windowBits < 16) || t && t.windowBits || (e.windowBits += 32), e.windowBits > 15 && e.windowBits < 48 && 0 === (15 & e.windowBits) && (e.windowBits |= 15), this.err = 0, this.msg = "", this.ended = !1, this.chunks = [], this.strm = new f, this.strm.avail_out = 0; var a = s.inflateInit2(this.strm, e.windowBits); if (a !== h.Z_OK) throw new Error(d[a]); this.header = new _, s.inflateGetHeader(this.strm, this.header) } function n(t, e) { var a = new i(e); if (a.push(t, !0), a.err) throw a.msg; return a.result } function r(t, e) { return e = e || {}, e.raw = !0, n(t, e) } var s = t("./zlib/inflate"), o = t("./utils/common"), l = t("./utils/strings"), h = t("./zlib/constants"), d = t("./zlib/messages"), f = t("./zlib/zstream"), _ = t("./zlib/gzheader"), u = Object.prototype.toString; i.prototype.push = function (t, e) { var a, i, n, r, d, f, _ = this.strm, c = this.options.chunkSize, b = this.options.dictionary, g = !1; if (this.ended) return !1; i = e === ~~e ? e : e === !0 ? h.Z_FINISH : h.Z_NO_FLUSH, "string" == typeof t ? _.input = l.binstring2buf(t) : "[object ArrayBuffer]" === u.call(t) ? _.input = new Uint8Array(t) : _.input = t, _.next_in = 0, _.avail_in = _.input.length; do { if (0 === _.avail_out && (_.output = new o.Buf8(c), _.next_out = 0, _.avail_out = c), a = s.inflate(_, h.Z_NO_FLUSH), a === h.Z_NEED_DICT && b && (f = "string" == typeof b ? l.string2buf(b) : "[object ArrayBuffer]" === u.call(b) ? new Uint8Array(b) : b, a = s.inflateSetDictionary(this.strm, f)), a === h.Z_BUF_ERROR && g === !0 && (a = h.Z_OK, g = !1), a !== h.Z_STREAM_END && a !== h.Z_OK) return this.onEnd(a), this.ended = !0, !1; _.next_out && (0 !== _.avail_out && a !== h.Z_STREAM_END && (0 !== _.avail_in || i !== h.Z_FINISH && i !== h.Z_SYNC_FLUSH) || ("string" === this.options.to ? (n = l.utf8border(_.output, _.next_out), r = _.next_out - n, d = l.buf2string(_.output, n), _.next_out = r, _.avail_out = c - r, r && o.arraySet(_.output, _.output, n, r, 0), this.onData(d)) : this.onData(o.shrinkBuf(_.output, _.next_out)))), 0 === _.avail_in && 0 === _.avail_out && (g = !0) } while ((_.avail_in > 0 || 0 === _.avail_out) && a !== h.Z_STREAM_END); return a === h.Z_STREAM_END && (i = h.Z_FINISH), i === h.Z_FINISH ? (a = s.inflateEnd(this.strm), this.onEnd(a), this.ended = !0, a === h.Z_OK) : i !== h.Z_SYNC_FLUSH || (this.onEnd(h.Z_OK), _.avail_out = 0, !0) }, i.prototype.onData = function (t) { this.chunks.push(t) }, i.prototype.onEnd = function (t) { t === h.Z_OK && ("string" === this.options.to ? this.result = this.chunks.join("") : this.result = o.flattenChunks(this.chunks)), this.chunks = [], this.err = t, this.msg = this.strm.msg }, a.Inflate = i, a.inflate = n, a.inflateRaw = r, a.ungzip = n }, { "./utils/common": 3, "./utils/strings": 4, "./zlib/constants": 6, "./zlib/gzheader": 9, "./zlib/inflate": 11, "./zlib/messages": 13, "./zlib/zstream": 15 }], 3: [function (t, e, a) { "use strict"; var i = "undefined" != typeof Uint8Array && "undefined" != typeof Uint16Array && "undefined" != typeof Int32Array; a.assign = function (t) { for (var e = Array.prototype.slice.call(arguments, 1); e.length;) { var a = e.shift(); if (a) { if ("object" != typeof a) throw new TypeError(a + "must be non-object"); for (var i in a) a.hasOwnProperty(i) && (t[i] = a[i]) } } return t }, a.shrinkBuf = function (t, e) { return t.length === e ? t : t.subarray ? t.subarray(0, e) : (t.length = e, t) }; var n = { arraySet: function (t, e, a, i, n) { if (e.subarray && t.subarray) return void t.set(e.subarray(a, a + i), n); for (var r = 0; r < i; r++)t[n + r] = e[a + r] }, flattenChunks: function (t) { var e, a, i, n, r, s; for (i = 0, e = 0, a = t.length; e < a; e++)i += t[e].length; for (s = new Uint8Array(i), n = 0, e = 0, a = t.length; e < a; e++)r = t[e], s.set(r, n), n += r.length; return s } }, r = { arraySet: function (t, e, a, i, n) { for (var r = 0; r < i; r++)t[n + r] = e[a + r] }, flattenChunks: function (t) { return [].concat.apply([], t) } }; a.setTyped = function (t) { t ? (a.Buf8 = Uint8Array, a.Buf16 = Uint16Array, a.Buf32 = Int32Array, a.assign(a, n)) : (a.Buf8 = Array, a.Buf16 = Array, a.Buf32 = Array, a.assign(a, r)) }, a.setTyped(i) }, {}], 4: [function (t, e, a) { "use strict"; function i(t, e) { if (e < 65537 && (t.subarray && s || !t.subarray && r)) return String.fromCharCode.apply(null, n.shrinkBuf(t, e)); for (var a = "", i = 0; i < e; i++)a += String.fromCharCode(t[i]); return a } var n = t("./common"), r = !0, s = !0; try { String.fromCharCode.apply(null, [0]) } catch (t) { r = !1 } try { String.fromCharCode.apply(null, new Uint8Array(1)) } catch (t) { s = !1 } for (var o = new n.Buf8(256), l = 0; l < 256; l++)o[l] = l >= 252 ? 6 : l >= 248 ? 5 : l >= 240 ? 4 : l >= 224 ? 3 : l >= 192 ? 2 : 1; o[254] = o[254] = 1, a.string2buf = function (t) { var e, a, i, r, s, o = t.length, l = 0; for (r = 0; r < o; r++)a = t.charCodeAt(r), 55296 === (64512 & a) && r + 1 < o && (i = t.charCodeAt(r + 1), 56320 === (64512 & i) && (a = 65536 + (a - 55296 << 10) + (i - 56320), r++)), l += a < 128 ? 1 : a < 2048 ? 2 : a < 65536 ? 3 : 4; for (e = new n.Buf8(l), s = 0, r = 0; s < l; r++)a = t.charCodeAt(r), 55296 === (64512 & a) && r + 1 < o && (i = t.charCodeAt(r + 1), 56320 === (64512 & i) && (a = 65536 + (a - 55296 << 10) + (i - 56320), r++)), a < 128 ? e[s++] = a : a < 2048 ? (e[s++] = 192 | a >>> 6, e[s++] = 128 | 63 & a) : a < 65536 ? (e[s++] = 224 | a >>> 12, e[s++] = 128 | a >>> 6 & 63, e[s++] = 128 | 63 & a) : (e[s++] = 240 | a >>> 18, e[s++] = 128 | a >>> 12 & 63, e[s++] = 128 | a >>> 6 & 63, e[s++] = 128 | 63 & a); return e }, a.buf2binstring = function (t) { return i(t, t.length) }, a.binstring2buf = function (t) { for (var e = new n.Buf8(t.length), a = 0, i = e.length; a < i; a++)e[a] = t.charCodeAt(a); return e }, a.buf2string = function (t, e) { var a, n, r, s, l = e || t.length, h = new Array(2 * l); for (n = 0, a = 0; a < l;)if (r = t[a++], r < 128) h[n++] = r; else if (s = o[r], s > 4) h[n++] = 65533, a += s - 1; else { for (r &= 2 === s ? 31 : 3 === s ? 15 : 7; s > 1 && a < l;)r = r << 6 | 63 & t[a++], s--; s > 1 ? h[n++] = 65533 : r < 65536 ? h[n++] = r : (r -= 65536, h[n++] = 55296 | r >> 10 & 1023, h[n++] = 56320 | 1023 & r) } return i(h, n) }, a.utf8border = function (t, e) { var a; for (e = e || t.length, e > t.length && (e = t.length), a = e - 1; a >= 0 && 128 === (192 & t[a]);)a--; return a < 0 ? e : 0 === a ? e : a + o[t[a]] > e ? a : e } }, { "./common": 3 }], 5: [function (t, e, a) { "use strict"; function i(t, e, a, i) { for (var n = 65535 & t | 0, r = t >>> 16 & 65535 | 0, s = 0; 0 !== a;) { s = a > 2e3 ? 2e3 : a, a -= s; do n = n + e[i++] | 0, r = r + n | 0; while (--s); n %= 65521, r %= 65521 } return n | r << 16 | 0 } e.exports = i }, {}], 6: [function (t, e, a) { "use strict"; e.exports = { Z_NO_FLUSH: 0, Z_PARTIAL_FLUSH: 1, Z_SYNC_FLUSH: 2, Z_FULL_FLUSH: 3, Z_FINISH: 4, Z_BLOCK: 5, Z_TREES: 6, Z_OK: 0, Z_STREAM_END: 1, Z_NEED_DICT: 2, Z_ERRNO: -1, Z_STREAM_ERROR: -2, Z_DATA_ERROR: -3, Z_BUF_ERROR: -5, Z_NO_COMPRESSION: 0, Z_BEST_SPEED: 1, Z_BEST_COMPRESSION: 9, Z_DEFAULT_COMPRESSION: -1, Z_FILTERED: 1, Z_HUFFMAN_ONLY: 2, Z_RLE: 3, Z_FIXED: 4, Z_DEFAULT_STRATEGY: 0, Z_BINARY: 0, Z_TEXT: 1, Z_UNKNOWN: 2, Z_DEFLATED: 8 } }, {}], 7: [function (t, e, a) { "use strict"; function i() { for (var t, e = [], a = 0; a < 256; a++) { t = a; for (var i = 0; i < 8; i++)t = 1 & t ? 3988292384 ^ t >>> 1 : t >>> 1; e[a] = t } return e } function n(t, e, a, i) { var n = r, s = i + a; t ^= -1; for (var o = i; o < s; o++)t = t >>> 8 ^ n[255 & (t ^ e[o])]; return t ^ -1 } var r = i(); e.exports = n }, {}], 8: [function (t, e, a) { "use strict"; function i(t, e) { return t.msg = D[e], e } function n(t) { return (t << 1) - (t > 4 ? 9 : 0) } function r(t) { for (var e = t.length; --e >= 0;)t[e] = 0 } function s(t) { var e = t.state, a = e.pending; a > t.avail_out && (a = t.avail_out), 0 !== a && (R.arraySet(t.output, e.pending_buf, e.pending_out, a, t.next_out), t.next_out += a, e.pending_out += a, t.total_out += a, t.avail_out -= a, e.pending -= a, 0 === e.pending && (e.pending_out = 0)) } function o(t, e) { C._tr_flush_block(t, t.block_start >= 0 ? t.block_start : -1, t.strstart - t.block_start, e), t.block_start = t.strstart, s(t.strm) } function l(t, e) { t.pending_buf[t.pending++] = e } function h(t, e) { t.pending_buf[t.pending++] = e >>> 8 & 255, t.pending_buf[t.pending++] = 255 & e } function d(t, e, a, i) { var n = t.avail_in; return n > i && (n = i), 0 === n ? 0 : (t.avail_in -= n, R.arraySet(e, t.input, t.next_in, n, a), 1 === t.state.wrap ? t.adler = N(t.adler, e, n, a) : 2 === t.state.wrap && (t.adler = O(t.adler, e, n, a)), t.next_in += n, t.total_in += n, n) } function f(t, e) { var a, i, n = t.max_chain_length, r = t.strstart, s = t.prev_length, o = t.nice_match, l = t.strstart > t.w_size - ft ? t.strstart - (t.w_size - ft) : 0, h = t.window, d = t.w_mask, f = t.prev, _ = t.strstart + dt, u = h[r + s - 1], c = h[r + s]; t.prev_length >= t.good_match && (n >>= 2), o > t.lookahead && (o = t.lookahead); do if (a = e, h[a + s] === c && h[a + s - 1] === u && h[a] === h[r] && h[++a] === h[r + 1]) { r += 2, a++; do; while (h[++r] === h[++a] && h[++r] === h[++a] && h[++r] === h[++a] && h[++r] === h[++a] && h[++r] === h[++a] && h[++r] === h[++a] && h[++r] === h[++a] && h[++r] === h[++a] && r < _); if (i = dt - (_ - r), r = _ - dt, i > s) { if (t.match_start = e, s = i, i >= o) break; u = h[r + s - 1], c = h[r + s] } } while ((e = f[e & d]) > l && 0 !== --n); return s <= t.lookahead ? s : t.lookahead } function _(t) { var e, a, i, n, r, s = t.w_size; do { if (n = t.window_size - t.lookahead - t.strstart, t.strstart >= s + (s - ft)) { R.arraySet(t.window, t.window, s, s, 0), t.match_start -= s, t.strstart -= s, t.block_start -= s, a = t.hash_size, e = a; do i = t.head[--e], t.head[e] = i >= s ? i - s : 0; while (--a); a = s, e = a; do i = t.prev[--e], t.prev[e] = i >= s ? i - s : 0; while (--a); n += s } if (0 === t.strm.avail_in) break; if (a = d(t.strm, t.window, t.strstart + t.lookahead, n), t.lookahead += a, t.lookahead + t.insert >= ht) for (r = t.strstart - t.insert, t.ins_h = t.window[r], t.ins_h = (t.ins_h << t.hash_shift ^ t.window[r + 1]) & t.hash_mask; t.insert && (t.ins_h = (t.ins_h << t.hash_shift ^ t.window[r + ht - 1]) & t.hash_mask, t.prev[r & t.w_mask] = t.head[t.ins_h], t.head[t.ins_h] = r, r++ , t.insert-- , !(t.lookahead + t.insert < ht));); } while (t.lookahead < ft && 0 !== t.strm.avail_in) } function u(t, e) { var a = 65535; for (a > t.pending_buf_size - 5 && (a = t.pending_buf_size - 5); ;) { if (t.lookahead <= 1) { if (_(t), 0 === t.lookahead && e === I) return vt; if (0 === t.lookahead) break } t.strstart += t.lookahead, t.lookahead = 0; var i = t.block_start + a; if ((0 === t.strstart || t.strstart >= i) && (t.lookahead = t.strstart - i, t.strstart = i, o(t, !1), 0 === t.strm.avail_out)) return vt; if (t.strstart - t.block_start >= t.w_size - ft && (o(t, !1), 0 === t.strm.avail_out)) return vt } return t.insert = 0, e === F ? (o(t, !0), 0 === t.strm.avail_out ? yt : xt) : t.strstart > t.block_start && (o(t, !1), 0 === t.strm.avail_out) ? vt : vt } function c(t, e) { for (var a, i; ;) { if (t.lookahead < ft) { if (_(t), t.lookahead < ft && e === I) return vt; if (0 === t.lookahead) break } if (a = 0, t.lookahead >= ht && (t.ins_h = (t.ins_h << t.hash_shift ^ t.window[t.strstart + ht - 1]) & t.hash_mask, a = t.prev[t.strstart & t.w_mask] = t.head[t.ins_h], t.head[t.ins_h] = t.strstart), 0 !== a && t.strstart - a <= t.w_size - ft && (t.match_length = f(t, a)), t.match_length >= ht) if (i = C._tr_tally(t, t.strstart - t.match_start, t.match_length - ht), t.lookahead -= t.match_length, t.match_length <= t.max_lazy_match && t.lookahead >= ht) { t.match_length--; do t.strstart++ , t.ins_h = (t.ins_h << t.hash_shift ^ t.window[t.strstart + ht - 1]) & t.hash_mask, a = t.prev[t.strstart & t.w_mask] = t.head[t.ins_h], t.head[t.ins_h] = t.strstart; while (0 !== --t.match_length); t.strstart++ } else t.strstart += t.match_length, t.match_length = 0, t.ins_h = t.window[t.strstart], t.ins_h = (t.ins_h << t.hash_shift ^ t.window[t.strstart + 1]) & t.hash_mask; else i = C._tr_tally(t, 0, t.window[t.strstart]), t.lookahead-- , t.strstart++; if (i && (o(t, !1), 0 === t.strm.avail_out)) return vt } return t.insert = t.strstart < ht - 1 ? t.strstart : ht - 1, e === F ? (o(t, !0), 0 === t.strm.avail_out ? yt : xt) : t.last_lit && (o(t, !1), 0 === t.strm.avail_out) ? vt : kt } function b(t, e) { for (var a, i, n; ;) { if (t.lookahead < ft) { if (_(t), t.lookahead < ft && e === I) return vt; if (0 === t.lookahead) break } if (a = 0, t.lookahead >= ht && (t.ins_h = (t.ins_h << t.hash_shift ^ t.window[t.strstart + ht - 1]) & t.hash_mask, a = t.prev[t.strstart & t.w_mask] = t.head[t.ins_h], t.head[t.ins_h] = t.strstart), t.prev_length = t.match_length, t.prev_match = t.match_start, t.match_length = ht - 1, 0 !== a && t.prev_length < t.max_lazy_match && t.strstart - a <= t.w_size - ft && (t.match_length = f(t, a), t.match_length <= 5 && (t.strategy === q || t.match_length === ht && t.strstart - t.match_start > 4096) && (t.match_length = ht - 1)), t.prev_length >= ht && t.match_length <= t.prev_length) { n = t.strstart + t.lookahead - ht, i = C._tr_tally(t, t.strstart - 1 - t.prev_match, t.prev_length - ht), t.lookahead -= t.prev_length - 1, t.prev_length -= 2; do ++t.strstart <= n && (t.ins_h = (t.ins_h << t.hash_shift ^ t.window[t.strstart + ht - 1]) & t.hash_mask, a = t.prev[t.strstart & t.w_mask] = t.head[t.ins_h], t.head[t.ins_h] = t.strstart); while (0 !== --t.prev_length); if (t.match_available = 0, t.match_length = ht - 1, t.strstart++ , i && (o(t, !1), 0 === t.strm.avail_out)) return vt } else if (t.match_available) { if (i = C._tr_tally(t, 0, t.window[t.strstart - 1]), i && o(t, !1), t.strstart++ , t.lookahead-- , 0 === t.strm.avail_out) return vt } else t.match_available = 1, t.strstart++ , t.lookahead-- } return t.match_available && (i = C._tr_tally(t, 0, t.window[t.strstart - 1]), t.match_available = 0), t.insert = t.strstart < ht - 1 ? t.strstart : ht - 1, e === F ? (o(t, !0), 0 === t.strm.avail_out ? yt : xt) : t.last_lit && (o(t, !1), 0 === t.strm.avail_out) ? vt : kt } function g(t, e) { for (var a, i, n, r, s = t.window; ;) { if (t.lookahead <= dt) { if (_(t), t.lookahead <= dt && e === I) return vt; if (0 === t.lookahead) break } if (t.match_length = 0, t.lookahead >= ht && t.strstart > 0 && (n = t.strstart - 1, i = s[n], i === s[++n] && i === s[++n] && i === s[++n])) { r = t.strstart + dt; do; while (i === s[++n] && i === s[++n] && i === s[++n] && i === s[++n] && i === s[++n] && i === s[++n] && i === s[++n] && i === s[++n] && n < r); t.match_length = dt - (r - n), t.match_length > t.lookahead && (t.match_length = t.lookahead) } if (t.match_length >= ht ? (a = C._tr_tally(t, 1, t.match_length - ht), t.lookahead -= t.match_length, t.strstart += t.match_length, t.match_length = 0) : (a = C._tr_tally(t, 0, t.window[t.strstart]), t.lookahead-- , t.strstart++), a && (o(t, !1), 0 === t.strm.avail_out)) return vt } return t.insert = 0, e === F ? (o(t, !0), 0 === t.strm.avail_out ? yt : xt) : t.last_lit && (o(t, !1), 0 === t.strm.avail_out) ? vt : kt } function m(t, e) { for (var a; ;) { if (0 === t.lookahead && (_(t), 0 === t.lookahead)) { if (e === I) return vt; break } if (t.match_length = 0, a = C._tr_tally(t, 0, t.window[t.strstart]), t.lookahead-- , t.strstart++ , a && (o(t, !1), 0 === t.strm.avail_out)) return vt } return t.insert = 0, e === F ? (o(t, !0), 0 === t.strm.avail_out ? yt : xt) : t.last_lit && (o(t, !1), 0 === t.strm.avail_out) ? vt : kt } function w(t, e, a, i, n) { this.good_length = t, this.max_lazy = e, this.nice_length = a, this.max_chain = i, this.func = n } function p(t) { t.window_size = 2 * t.w_size, r(t.head), t.max_lazy_match = Z[t.level].max_lazy, t.good_match = Z[t.level].good_length, t.nice_match = Z[t.level].nice_length, t.max_chain_length = Z[t.level].max_chain, t.strstart = 0, t.block_start = 0, t.lookahead = 0, t.insert = 0, t.match_length = t.prev_length = ht - 1, t.match_available = 0, t.ins_h = 0 } function v() { this.strm = null, this.status = 0, this.pending_buf = null, this.pending_buf_size = 0, this.pending_out = 0, this.pending = 0, this.wrap = 0, this.gzhead = null, this.gzindex = 0, this.method = V, this.last_flush = -1, this.w_size = 0, this.w_bits = 0, this.w_mask = 0, this.window = null, this.window_size = 0, this.prev = null, this.head = null, this.ins_h = 0, this.hash_size = 0, this.hash_bits = 0, this.hash_mask = 0, this.hash_shift = 0, this.block_start = 0, this.match_length = 0, this.prev_match = 0, this.match_available = 0, this.strstart = 0, this.match_start = 0, this.lookahead = 0, this.prev_length = 0, this.max_chain_length = 0, this.max_lazy_match = 0, this.level = 0, this.strategy = 0, this.good_match = 0, this.nice_match = 0, this.dyn_ltree = new R.Buf16(2 * ot), this.dyn_dtree = new R.Buf16(2 * (2 * rt + 1)), this.bl_tree = new R.Buf16(2 * (2 * st + 1)), r(this.dyn_ltree), r(this.dyn_dtree), r(this.bl_tree), this.l_desc = null, this.d_desc = null, this.bl_desc = null, this.bl_count = new R.Buf16(lt + 1), this.heap = new R.Buf16(2 * nt + 1), r(this.heap), this.heap_len = 0, this.heap_max = 0, this.depth = new R.Buf16(2 * nt + 1), r(this.depth), this.l_buf = 0, this.lit_bufsize = 0, this.last_lit = 0, this.d_buf = 0, this.opt_len = 0, this.static_len = 0, this.matches = 0, this.insert = 0, this.bi_buf = 0, this.bi_valid = 0 } function k(t) { var e; return t && t.state ? (t.total_in = t.total_out = 0, t.data_type = Q, e = t.state, e.pending = 0, e.pending_out = 0, e.wrap < 0 && (e.wrap = -e.wrap), e.status = e.wrap ? ut : wt, t.adler = 2 === e.wrap ? 0 : 1, e.last_flush = I, C._tr_init(e), H) : i(t, K) } function y(t) { var e = k(t); return e === H && p(t.state), e } function x(t, e) { return t && t.state ? 2 !== t.state.wrap ? K : (t.state.gzhead = e, H) : K } function z(t, e, a, n, r, s) { if (!t) return K; var o = 1; if (e === Y && (e = 6), n < 0 ? (o = 0, n = -n) : n > 15 && (o = 2, n -= 16), r < 1 || r > $ || a !== V || n < 8 || n > 15 || e < 0 || e > 9 || s < 0 || s > W) return i(t, K); 8 === n && (n = 9); var l = new v; return t.state = l, l.strm = t, l.wrap = o, l.gzhead = null, l.w_bits = n, l.w_size = 1 << l.w_bits, l.w_mask = l.w_size - 1, l.hash_bits = r + 7, l.hash_size = 1 << l.hash_bits, l.hash_mask = l.hash_size - 1, l.hash_shift = ~~((l.hash_bits + ht - 1) / ht), l.window = new R.Buf8(2 * l.w_size), l.head = new R.Buf16(l.hash_size), l.prev = new R.Buf16(l.w_size), l.lit_bufsize = 1 << r + 6, l.pending_buf_size = 4 * l.lit_bufsize, l.pending_buf = new R.Buf8(l.pending_buf_size), l.d_buf = 1 * l.lit_bufsize, l.l_buf = 3 * l.lit_bufsize, l.level = e, l.strategy = s, l.method = a, y(t) } function B(t, e) { return z(t, e, V, tt, et, J) } function S(t, e) { var a, o, d, f; if (!t || !t.state || e > L || e < 0) return t ? i(t, K) : K; if (o = t.state, !t.output || !t.input && 0 !== t.avail_in || o.status === pt && e !== F) return i(t, 0 === t.avail_out ? P : K); if (o.strm = t, a = o.last_flush, o.last_flush = e, o.status === ut) if (2 === o.wrap) t.adler = 0, l(o, 31), l(o, 139), l(o, 8), o.gzhead ? (l(o, (o.gzhead.text ? 1 : 0) + (o.gzhead.hcrc ? 2 : 0) + (o.gzhead.extra ? 4 : 0) + (o.gzhead.name ? 8 : 0) + (o.gzhead.comment ? 16 : 0)), l(o, 255 & o.gzhead.time), l(o, o.gzhead.time >> 8 & 255), l(o, o.gzhead.time >> 16 & 255), l(o, o.gzhead.time >> 24 & 255), l(o, 9 === o.level ? 2 : o.strategy >= G || o.level < 2 ? 4 : 0), l(o, 255 & o.gzhead.os), o.gzhead.extra && o.gzhead.extra.length && (l(o, 255 & o.gzhead.extra.length), l(o, o.gzhead.extra.length >> 8 & 255)), o.gzhead.hcrc && (t.adler = O(t.adler, o.pending_buf, o.pending, 0)), o.gzindex = 0, o.status = ct) : (l(o, 0), l(o, 0), l(o, 0), l(o, 0), l(o, 0), l(o, 9 === o.level ? 2 : o.strategy >= G || o.level < 2 ? 4 : 0), l(o, zt), o.status = wt); else { var _ = V + (o.w_bits - 8 << 4) << 8, u = -1; u = o.strategy >= G || o.level < 2 ? 0 : o.level < 6 ? 1 : 6 === o.level ? 2 : 3, _ |= u << 6, 0 !== o.strstart && (_ |= _t), _ += 31 - _ % 31, o.status = wt, h(o, _), 0 !== o.strstart && (h(o, t.adler >>> 16), h(o, 65535 & t.adler)), t.adler = 1 } if (o.status === ct) if (o.gzhead.extra) { for (d = o.pending; o.gzindex < (65535 & o.gzhead.extra.length) && (o.pending !== o.pending_buf_size || (o.gzhead.hcrc && o.pending > d && (t.adler = O(t.adler, o.pending_buf, o.pending - d, d)), s(t), d = o.pending, o.pending !== o.pending_buf_size));)l(o, 255 & o.gzhead.extra[o.gzindex]), o.gzindex++; o.gzhead.hcrc && o.pending > d && (t.adler = O(t.adler, o.pending_buf, o.pending - d, d)), o.gzindex === o.gzhead.extra.length && (o.gzindex = 0, o.status = bt) } else o.status = bt; if (o.status === bt) if (o.gzhead.name) { d = o.pending; do { if (o.pending === o.pending_buf_size && (o.gzhead.hcrc && o.pending > d && (t.adler = O(t.adler, o.pending_buf, o.pending - d, d)), s(t), d = o.pending, o.pending === o.pending_buf_size)) { f = 1; break } f = o.gzindex < o.gzhead.name.length ? 255 & o.gzhead.name.charCodeAt(o.gzindex++) : 0, l(o, f) } while (0 !== f); o.gzhead.hcrc && o.pending > d && (t.adler = O(t.adler, o.pending_buf, o.pending - d, d)), 0 === f && (o.gzindex = 0, o.status = gt) } else o.status = gt; if (o.status === gt) if (o.gzhead.comment) { d = o.pending; do { if (o.pending === o.pending_buf_size && (o.gzhead.hcrc && o.pending > d && (t.adler = O(t.adler, o.pending_buf, o.pending - d, d)), s(t), d = o.pending, o.pending === o.pending_buf_size)) { f = 1; break } f = o.gzindex < o.gzhead.comment.length ? 255 & o.gzhead.comment.charCodeAt(o.gzindex++) : 0, l(o, f) } while (0 !== f); o.gzhead.hcrc && o.pending > d && (t.adler = O(t.adler, o.pending_buf, o.pending - d, d)), 0 === f && (o.status = mt) } else o.status = mt; if (o.status === mt && (o.gzhead.hcrc ? (o.pending + 2 > o.pending_buf_size && s(t), o.pending + 2 <= o.pending_buf_size && (l(o, 255 & t.adler), l(o, t.adler >> 8 & 255), t.adler = 0, o.status = wt)) : o.status = wt), 0 !== o.pending) { if (s(t), 0 === t.avail_out) return o.last_flush = -1, H } else if (0 === t.avail_in && n(e) <= n(a) && e !== F) return i(t, P); if (o.status === pt && 0 !== t.avail_in) return i(t, P); if (0 !== t.avail_in || 0 !== o.lookahead || e !== I && o.status !== pt) { var c = o.strategy === G ? m(o, e) : o.strategy === X ? g(o, e) : Z[o.level].func(o, e); if (c !== yt && c !== xt || (o.status = pt), c === vt || c === yt) return 0 === t.avail_out && (o.last_flush = -1), H; if (c === kt && (e === U ? C._tr_align(o) : e !== L && (C._tr_stored_block(o, 0, 0, !1), e === T && (r(o.head), 0 === o.lookahead && (o.strstart = 0, o.block_start = 0, o.insert = 0))), s(t), 0 === t.avail_out)) return o.last_flush = -1, H } return e !== F ? H : o.wrap <= 0 ? j : (2 === o.wrap ? (l(o, 255 & t.adler), l(o, t.adler >> 8 & 255), l(o, t.adler >> 16 & 255), l(o, t.adler >> 24 & 255), l(o, 255 & t.total_in), l(o, t.total_in >> 8 & 255), l(o, t.total_in >> 16 & 255), l(o, t.total_in >> 24 & 255)) : (h(o, t.adler >>> 16), h(o, 65535 & t.adler)), s(t), o.wrap > 0 && (o.wrap = -o.wrap), 0 !== o.pending ? H : j) } function E(t) { var e; return t && t.state ? (e = t.state.status, e !== ut && e !== ct && e !== bt && e !== gt && e !== mt && e !== wt && e !== pt ? i(t, K) : (t.state = null, e === wt ? i(t, M) : H)) : K } function A(t, e) { var a, i, n, s, o, l, h, d, f = e.length; if (!t || !t.state) return K; if (a = t.state, s = a.wrap, 2 === s || 1 === s && a.status !== ut || a.lookahead) return K; for (1 === s && (t.adler = N(t.adler, e, f, 0)), a.wrap = 0, f >= a.w_size && (0 === s && (r(a.head), a.strstart = 0, a.block_start = 0, a.insert = 0), d = new R.Buf8(a.w_size), R.arraySet(d, e, f - a.w_size, a.w_size, 0), e = d, f = a.w_size), o = t.avail_in, l = t.next_in, h = t.input, t.avail_in = f, t.next_in = 0, t.input = e, _(a); a.lookahead >= ht;) { i = a.strstart, n = a.lookahead - (ht - 1); do a.ins_h = (a.ins_h << a.hash_shift ^ a.window[i + ht - 1]) & a.hash_mask, a.prev[i & a.w_mask] = a.head[a.ins_h], a.head[a.ins_h] = i, i++; while (--n); a.strstart = i, a.lookahead = ht - 1, _(a) } return a.strstart += a.lookahead, a.block_start = a.strstart, a.insert = a.lookahead, a.lookahead = 0, a.match_length = a.prev_length = ht - 1, a.match_available = 0, t.next_in = l, t.input = h, t.avail_in = o, a.wrap = s, H } var Z, R = t("../utils/common"), C = t("./trees"), N = t("./adler32"), O = t("./crc32"), D = t("./messages"), I = 0, U = 1, T = 3, F = 4, L = 5, H = 0, j = 1, K = -2, M = -3, P = -5, Y = -1, q = 1, G = 2, X = 3, W = 4, J = 0, Q = 2, V = 8, $ = 9, tt = 15, et = 8, at = 29, it = 256, nt = it + 1 + at, rt = 30, st = 19, ot = 2 * nt + 1, lt = 15, ht = 3, dt = 258, ft = dt + ht + 1, _t = 32, ut = 42, ct = 69, bt = 73, gt = 91, mt = 103, wt = 113, pt = 666, vt = 1, kt = 2, yt = 3, xt = 4, zt = 3; Z = [new w(0, 0, 0, 0, u), new w(4, 4, 8, 4, c), new w(4, 5, 16, 8, c), new w(4, 6, 32, 32, c), new w(4, 4, 16, 16, b), new w(8, 16, 32, 32, b), new w(8, 16, 128, 128, b), new w(8, 32, 128, 256, b), new w(32, 128, 258, 1024, b), new w(32, 258, 258, 4096, b)], a.deflateInit = B, a.deflateInit2 = z, a.deflateReset = y, a.deflateResetKeep = k, a.deflateSetHeader = x, a.deflate = S, a.deflateEnd = E, a.deflateSetDictionary = A, a.deflateInfo = "pako deflate (from Nodeca project)" }, { "../utils/common": 3, "./adler32": 5, "./crc32": 7, "./messages": 13, "./trees": 14 }], 9: [function (t, e, a) { "use strict"; function i() { this.text = 0, this.time = 0, this.xflags = 0, this.os = 0, this.extra = null, this.extra_len = 0, this.name = "", this.comment = "", this.hcrc = 0, this.done = !1 } e.exports = i }, {}], 10: [function (t, e, a) { "use strict"; var i = 30, n = 12; e.exports = function (t, e) { var a, r, s, o, l, h, d, f, _, u, c, b, g, m, w, p, v, k, y, x, z, B, S, E, A; a = t.state, r = t.next_in, E = t.input, s = r + (t.avail_in - 5), o = t.next_out, A = t.output, l = o - (e - t.avail_out), h = o + (t.avail_out - 257), d = a.dmax, f = a.wsize, _ = a.whave, u = a.wnext, c = a.window, b = a.hold, g = a.bits, m = a.lencode, w = a.distcode, p = (1 << a.lenbits) - 1, v = (1 << a.distbits) - 1; t: do { g < 15 && (b += E[r++] << g, g += 8, b += E[r++] << g, g += 8), k = m[b & p]; e: for (; ;) { if (y = k >>> 24, b >>>= y, g -= y, y = k >>> 16 & 255, 0 === y) A[o++] = 65535 & k; else { if (!(16 & y)) { if (0 === (64 & y)) { k = m[(65535 & k) + (b & (1 << y) - 1)]; continue e } if (32 & y) { a.mode = n; break t } t.msg = "invalid literal/length code", a.mode = i; break t } x = 65535 & k, y &= 15, y && (g < y && (b += E[r++] << g, g += 8), x += b & (1 << y) - 1, b >>>= y, g -= y), g < 15 && (b += E[r++] << g, g += 8, b += E[r++] << g, g += 8), k = w[b & v]; a: for (; ;) { if (y = k >>> 24, b >>>= y, g -= y, y = k >>> 16 & 255, !(16 & y)) { if (0 === (64 & y)) { k = w[(65535 & k) + (b & (1 << y) - 1)]; continue a } t.msg = "invalid distance code", a.mode = i; break t } if (z = 65535 & k, y &= 15, g < y && (b += E[r++] << g, g += 8, g < y && (b += E[r++] << g, g += 8)), z += b & (1 << y) - 1, z > d) { t.msg = "invalid distance too far back", a.mode = i; break t } if (b >>>= y, g -= y, y = o - l, z > y) { if (y = z - y, y > _ && a.sane) { t.msg = "invalid distance too far back", a.mode = i; break t } if (B = 0, S = c, 0 === u) { if (B += f - y, y < x) { x -= y; do A[o++] = c[B++]; while (--y); B = o - z, S = A } } else if (u < y) { if (B += f + u - y, y -= u, y < x) { x -= y; do A[o++] = c[B++]; while (--y); if (B = 0, u < x) { y = u, x -= y; do A[o++] = c[B++]; while (--y); B = o - z, S = A } } } else if (B += u - y, y < x) { x -= y; do A[o++] = c[B++]; while (--y); B = o - z, S = A } for (; x > 2;)A[o++] = S[B++], A[o++] = S[B++], A[o++] = S[B++], x -= 3; x && (A[o++] = S[B++], x > 1 && (A[o++] = S[B++])) } else { B = o - z; do A[o++] = A[B++], A[o++] = A[B++], A[o++] = A[B++], x -= 3; while (x > 2); x && (A[o++] = A[B++], x > 1 && (A[o++] = A[B++])) } break } } break } } while (r < s && o < h); x = g >> 3, r -= x, g -= x << 3, b &= (1 << g) - 1, t.next_in = r, t.next_out = o, t.avail_in = r < s ? 5 + (s - r) : 5 - (r - s), t.avail_out = o < h ? 257 + (h - o) : 257 - (o - h), a.hold = b, a.bits = g } }, {}], 11: [function (t, e, a) {
            "use strict"; function i(t) { return (t >>> 24 & 255) + (t >>> 8 & 65280) + ((65280 & t) << 8) + ((255 & t) << 24) } function n() { this.mode = 0, this.last = !1, this.wrap = 0, this.havedict = !1, this.flags = 0, this.dmax = 0, this.check = 0, this.total = 0, this.head = null, this.wbits = 0, this.wsize = 0, this.whave = 0, this.wnext = 0, this.window = null, this.hold = 0, this.bits = 0, this.length = 0, this.offset = 0, this.extra = 0, this.lencode = null, this.distcode = null, this.lenbits = 0, this.distbits = 0, this.ncode = 0, this.nlen = 0, this.ndist = 0, this.have = 0, this.next = null, this.lens = new w.Buf16(320), this.work = new w.Buf16(288), this.lendyn = null, this.distdyn = null, this.sane = 0, this.back = 0, this.was = 0 } function r(t) { var e; return t && t.state ? (e = t.state, t.total_in = t.total_out = e.total = 0, t.msg = "", e.wrap && (t.adler = 1 & e.wrap), e.mode = T, e.last = 0, e.havedict = 0, e.dmax = 32768, e.head = null, e.hold = 0, e.bits = 0, e.lencode = e.lendyn = new w.Buf32(bt), e.distcode = e.distdyn = new w.Buf32(gt), e.sane = 1, e.back = -1, Z) : N } function s(t) { var e; return t && t.state ? (e = t.state, e.wsize = 0, e.whave = 0, e.wnext = 0, r(t)) : N } function o(t, e) { var a, i; return t && t.state ? (i = t.state, e < 0 ? (a = 0, e = -e) : (a = (e >> 4) + 1, e < 48 && (e &= 15)), e && (e < 8 || e > 15) ? N : (null !== i.window && i.wbits !== e && (i.window = null), i.wrap = a, i.wbits = e, s(t))) : N } function l(t, e) { var a, i; return t ? (i = new n, t.state = i, i.window = null, a = o(t, e), a !== Z && (t.state = null), a) : N } function h(t) { return l(t, wt) } function d(t) { if (pt) { var e; for (g = new w.Buf32(512), m = new w.Buf32(32), e = 0; e < 144;)t.lens[e++] = 8; for (; e < 256;)t.lens[e++] = 9; for (; e < 280;)t.lens[e++] = 7; for (; e < 288;)t.lens[e++] = 8; for (y(z, t.lens, 0, 288, g, 0, t.work, { bits: 9 }), e = 0; e < 32;)t.lens[e++] = 5; y(B, t.lens, 0, 32, m, 0, t.work, { bits: 5 }), pt = !1 } t.lencode = g, t.lenbits = 9, t.distcode = m, t.distbits = 5 } function f(t, e, a, i) { var n, r = t.state; return null === r.window && (r.wsize = 1 << r.wbits, r.wnext = 0, r.whave = 0, r.window = new w.Buf8(r.wsize)), i >= r.wsize ? (w.arraySet(r.window, e, a - r.wsize, r.wsize, 0), r.wnext = 0, r.whave = r.wsize) : (n = r.wsize - r.wnext, n > i && (n = i), w.arraySet(r.window, e, a - i, n, r.wnext), i -= n, i ? (w.arraySet(r.window, e, a - i, i, 0), r.wnext = i, r.whave = r.wsize) : (r.wnext += n, r.wnext === r.wsize && (r.wnext = 0), r.whave < r.wsize && (r.whave += n))), 0 } function _(t, e) {
                var a, n, r, s, o, l, h, _, u, c, b, g, m, bt, gt, mt, wt, pt, vt, kt, yt, xt, zt, Bt, St = 0, Et = new w.Buf8(4), At = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15]; if (!t || !t.state || !t.output || !t.input && 0 !== t.avail_in) return N; a = t.state, a.mode === X && (a.mode = W), o = t.next_out, r = t.output, h = t.avail_out, s = t.next_in, n = t.input, l = t.avail_in, _ = a.hold, u = a.bits, c = l, b = h, xt = Z; t: for (; ;)switch (a.mode) {
                    case T: if (0 === a.wrap) { a.mode = W; break } for (; u < 16;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } if (2 & a.wrap && 35615 === _) { a.check = 0, Et[0] = 255 & _, Et[1] = _ >>> 8 & 255, a.check = v(a.check, Et, 2, 0), _ = 0, u = 0, a.mode = F; break } if (a.flags = 0, a.head && (a.head.done = !1), !(1 & a.wrap) || (((255 & _) << 8) + (_ >> 8)) % 31) { t.msg = "incorrect header check", a.mode = _t; break } if ((15 & _) !== U) { t.msg = "unknown compression method", a.mode = _t; break } if (_ >>>= 4, u -= 4, yt = (15 & _) + 8, 0 === a.wbits) a.wbits = yt; else if (yt > a.wbits) { t.msg = "invalid window size", a.mode = _t; break } a.dmax = 1 << yt, t.adler = a.check = 1, a.mode = 512 & _ ? q : X, _ = 0, u = 0; break; case F: for (; u < 16;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } if (a.flags = _, (255 & a.flags) !== U) { t.msg = "unknown compression method", a.mode = _t; break } if (57344 & a.flags) { t.msg = "unknown header flags set", a.mode = _t; break } a.head && (a.head.text = _ >> 8 & 1), 512 & a.flags && (Et[0] = 255 & _, Et[1] = _ >>> 8 & 255, a.check = v(a.check, Et, 2, 0)), _ = 0, u = 0, a.mode = L; case L: for (; u < 32;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } a.head && (a.head.time = _), 512 & a.flags && (Et[0] = 255 & _, Et[1] = _ >>> 8 & 255, Et[2] = _ >>> 16 & 255, Et[3] = _ >>> 24 & 255, a.check = v(a.check, Et, 4, 0)), _ = 0, u = 0, a.mode = H; case H: for (; u < 16;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } a.head && (a.head.xflags = 255 & _, a.head.os = _ >> 8), 512 & a.flags && (Et[0] = 255 & _, Et[1] = _ >>> 8 & 255, a.check = v(a.check, Et, 2, 0)), _ = 0, u = 0, a.mode = j; case j: if (1024 & a.flags) { for (; u < 16;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } a.length = _, a.head && (a.head.extra_len = _), 512 & a.flags && (Et[0] = 255 & _, Et[1] = _ >>> 8 & 255, a.check = v(a.check, Et, 2, 0)), _ = 0, u = 0 } else a.head && (a.head.extra = null); a.mode = K; case K: if (1024 & a.flags && (g = a.length, g > l && (g = l), g && (a.head && (yt = a.head.extra_len - a.length, a.head.extra || (a.head.extra = new Array(a.head.extra_len)), w.arraySet(a.head.extra, n, s, g, yt)), 512 & a.flags && (a.check = v(a.check, n, g, s)), l -= g, s += g, a.length -= g), a.length)) break t; a.length = 0, a.mode = M; case M: if (2048 & a.flags) { if (0 === l) break t; g = 0; do yt = n[s + g++], a.head && yt && a.length < 65536 && (a.head.name += String.fromCharCode(yt)); while (yt && g < l); if (512 & a.flags && (a.check = v(a.check, n, g, s)), l -= g, s += g, yt) break t } else a.head && (a.head.name = null); a.length = 0, a.mode = P; case P: if (4096 & a.flags) { if (0 === l) break t; g = 0; do yt = n[s + g++], a.head && yt && a.length < 65536 && (a.head.comment += String.fromCharCode(yt)); while (yt && g < l); if (512 & a.flags && (a.check = v(a.check, n, g, s)), l -= g, s += g, yt) break t } else a.head && (a.head.comment = null); a.mode = Y; case Y: if (512 & a.flags) { for (; u < 16;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } if (_ !== (65535 & a.check)) { t.msg = "header crc mismatch", a.mode = _t; break } _ = 0, u = 0 } a.head && (a.head.hcrc = a.flags >> 9 & 1, a.head.done = !0), t.adler = a.check = 0, a.mode = X; break; case q: for (; u < 32;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } t.adler = a.check = i(_), _ = 0, u = 0, a.mode = G; case G: if (0 === a.havedict) return t.next_out = o, t.avail_out = h, t.next_in = s, t.avail_in = l, a.hold = _, a.bits = u, C; t.adler = a.check = 1, a.mode = X; case X: if (e === E || e === A) break t; case W: if (a.last) { _ >>>= 7 & u, u -= 7 & u, a.mode = ht; break } for (; u < 3;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } switch (a.last = 1 & _, _ >>>= 1, u -= 1, 3 & _) { case 0: a.mode = J; break; case 1: if (d(a), a.mode = at, e === A) { _ >>>= 2, u -= 2; break t } break; case 2: a.mode = $; break; case 3: t.msg = "invalid block type", a.mode = _t }_ >>>= 2, u -= 2; break; case J: for (_ >>>= 7 & u, u -= 7 & u; u < 32;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } if ((65535 & _) !== (_ >>> 16 ^ 65535)) { t.msg = "invalid stored block lengths", a.mode = _t; break } if (a.length = 65535 & _, _ = 0, u = 0, a.mode = Q, e === A) break t; case Q: a.mode = V; case V: if (g = a.length) { if (g > l && (g = l), g > h && (g = h), 0 === g) break t; w.arraySet(r, n, s, g, o), l -= g, s += g, h -= g, o += g, a.length -= g; break } a.mode = X; break; case $: for (; u < 14;) {
                        if (0 === l) break t;
                        l-- , _ += n[s++] << u, u += 8
                    } if (a.nlen = (31 & _) + 257, _ >>>= 5, u -= 5, a.ndist = (31 & _) + 1, _ >>>= 5, u -= 5, a.ncode = (15 & _) + 4, _ >>>= 4, u -= 4, a.nlen > 286 || a.ndist > 30) { t.msg = "too many length or distance symbols", a.mode = _t; break } a.have = 0, a.mode = tt; case tt: for (; a.have < a.ncode;) { for (; u < 3;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } a.lens[At[a.have++]] = 7 & _, _ >>>= 3, u -= 3 } for (; a.have < 19;)a.lens[At[a.have++]] = 0; if (a.lencode = a.lendyn, a.lenbits = 7, zt = { bits: a.lenbits }, xt = y(x, a.lens, 0, 19, a.lencode, 0, a.work, zt), a.lenbits = zt.bits, xt) { t.msg = "invalid code lengths set", a.mode = _t; break } a.have = 0, a.mode = et; case et: for (; a.have < a.nlen + a.ndist;) { for (; St = a.lencode[_ & (1 << a.lenbits) - 1], gt = St >>> 24, mt = St >>> 16 & 255, wt = 65535 & St, !(gt <= u);) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } if (wt < 16) _ >>>= gt, u -= gt, a.lens[a.have++] = wt; else { if (16 === wt) { for (Bt = gt + 2; u < Bt;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } if (_ >>>= gt, u -= gt, 0 === a.have) { t.msg = "invalid bit length repeat", a.mode = _t; break } yt = a.lens[a.have - 1], g = 3 + (3 & _), _ >>>= 2, u -= 2 } else if (17 === wt) { for (Bt = gt + 3; u < Bt;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } _ >>>= gt, u -= gt, yt = 0, g = 3 + (7 & _), _ >>>= 3, u -= 3 } else { for (Bt = gt + 7; u < Bt;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } _ >>>= gt, u -= gt, yt = 0, g = 11 + (127 & _), _ >>>= 7, u -= 7 } if (a.have + g > a.nlen + a.ndist) { t.msg = "invalid bit length repeat", a.mode = _t; break } for (; g--;)a.lens[a.have++] = yt } } if (a.mode === _t) break; if (0 === a.lens[256]) { t.msg = "invalid code -- missing end-of-block", a.mode = _t; break } if (a.lenbits = 9, zt = { bits: a.lenbits }, xt = y(z, a.lens, 0, a.nlen, a.lencode, 0, a.work, zt), a.lenbits = zt.bits, xt) { t.msg = "invalid literal/lengths set", a.mode = _t; break } if (a.distbits = 6, a.distcode = a.distdyn, zt = { bits: a.distbits }, xt = y(B, a.lens, a.nlen, a.ndist, a.distcode, 0, a.work, zt), a.distbits = zt.bits, xt) { t.msg = "invalid distances set", a.mode = _t; break } if (a.mode = at, e === A) break t; case at: a.mode = it; case it: if (l >= 6 && h >= 258) { t.next_out = o, t.avail_out = h, t.next_in = s, t.avail_in = l, a.hold = _, a.bits = u, k(t, b), o = t.next_out, r = t.output, h = t.avail_out, s = t.next_in, n = t.input, l = t.avail_in, _ = a.hold, u = a.bits, a.mode === X && (a.back = -1); break } for (a.back = 0; St = a.lencode[_ & (1 << a.lenbits) - 1], gt = St >>> 24, mt = St >>> 16 & 255, wt = 65535 & St, !(gt <= u);) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } if (mt && 0 === (240 & mt)) { for (pt = gt, vt = mt, kt = wt; St = a.lencode[kt + ((_ & (1 << pt + vt) - 1) >> pt)], gt = St >>> 24, mt = St >>> 16 & 255, wt = 65535 & St, !(pt + gt <= u);) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } _ >>>= pt, u -= pt, a.back += pt } if (_ >>>= gt, u -= gt, a.back += gt, a.length = wt, 0 === mt) { a.mode = lt; break } if (32 & mt) { a.back = -1, a.mode = X; break } if (64 & mt) { t.msg = "invalid literal/length code", a.mode = _t; break } a.extra = 15 & mt, a.mode = nt; case nt: if (a.extra) { for (Bt = a.extra; u < Bt;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } a.length += _ & (1 << a.extra) - 1, _ >>>= a.extra, u -= a.extra, a.back += a.extra } a.was = a.length, a.mode = rt; case rt: for (; St = a.distcode[_ & (1 << a.distbits) - 1], gt = St >>> 24, mt = St >>> 16 & 255, wt = 65535 & St, !(gt <= u);) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } if (0 === (240 & mt)) { for (pt = gt, vt = mt, kt = wt; St = a.distcode[kt + ((_ & (1 << pt + vt) - 1) >> pt)], gt = St >>> 24, mt = St >>> 16 & 255, wt = 65535 & St, !(pt + gt <= u);) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } _ >>>= pt, u -= pt, a.back += pt } if (_ >>>= gt, u -= gt, a.back += gt, 64 & mt) { t.msg = "invalid distance code", a.mode = _t; break } a.offset = wt, a.extra = 15 & mt, a.mode = st; case st: if (a.extra) { for (Bt = a.extra; u < Bt;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } a.offset += _ & (1 << a.extra) - 1, _ >>>= a.extra, u -= a.extra, a.back += a.extra } if (a.offset > a.dmax) { t.msg = "invalid distance too far back", a.mode = _t; break } a.mode = ot; case ot: if (0 === h) break t; if (g = b - h, a.offset > g) { if (g = a.offset - g, g > a.whave && a.sane) { t.msg = "invalid distance too far back", a.mode = _t; break } g > a.wnext ? (g -= a.wnext, m = a.wsize - g) : m = a.wnext - g, g > a.length && (g = a.length), bt = a.window } else bt = r, m = o - a.offset, g = a.length; g > h && (g = h), h -= g, a.length -= g; do r[o++] = bt[m++]; while (--g); 0 === a.length && (a.mode = it); break; case lt: if (0 === h) break t; r[o++] = a.length, h-- , a.mode = it; break; case ht: if (a.wrap) { for (; u < 32;) { if (0 === l) break t; l-- , _ |= n[s++] << u, u += 8 } if (b -= h, t.total_out += b, a.total += b, b && (t.adler = a.check = a.flags ? v(a.check, r, b, o - b) : p(a.check, r, b, o - b)), b = h, (a.flags ? _ : i(_)) !== a.check) { t.msg = "incorrect data check", a.mode = _t; break } _ = 0, u = 0 } a.mode = dt; case dt: if (a.wrap && a.flags) { for (; u < 32;) { if (0 === l) break t; l-- , _ += n[s++] << u, u += 8 } if (_ !== (4294967295 & a.total)) { t.msg = "incorrect length check", a.mode = _t; break } _ = 0, u = 0 } a.mode = ft; case ft: xt = R; break t; case _t: xt = O; break t; case ut: return D; case ct: default: return N
                }return t.next_out = o, t.avail_out = h, t.next_in = s, t.avail_in = l, a.hold = _, a.bits = u, (a.wsize || b !== t.avail_out && a.mode < _t && (a.mode < ht || e !== S)) && f(t, t.output, t.next_out, b - t.avail_out) ? (a.mode = ut, D) : (c -= t.avail_in, b -= t.avail_out, t.total_in += c, t.total_out += b, a.total += b, a.wrap && b && (t.adler = a.check = a.flags ? v(a.check, r, b, t.next_out - b) : p(a.check, r, b, t.next_out - b)), t.data_type = a.bits + (a.last ? 64 : 0) + (a.mode === X ? 128 : 0) + (a.mode === at || a.mode === Q ? 256 : 0), (0 === c && 0 === b || e === S) && xt === Z && (xt = I), xt)
            } function u(t) { if (!t || !t.state) return N; var e = t.state; return e.window && (e.window = null), t.state = null, Z } function c(t, e) { var a; return t && t.state ? (a = t.state, 0 === (2 & a.wrap) ? N : (a.head = e, e.done = !1, Z)) : N } function b(t, e) { var a, i, n, r = e.length; return t && t.state ? (a = t.state, 0 !== a.wrap && a.mode !== G ? N : a.mode === G && (i = 1, i = p(i, e, r, 0), i !== a.check) ? O : (n = f(t, e, r, r)) ? (a.mode = ut, D) : (a.havedict = 1, Z)) : N } var g, m, w = t("../utils/common"), p = t("./adler32"), v = t("./crc32"), k = t("./inffast"), y = t("./inftrees"), x = 0, z = 1, B = 2, S = 4, E = 5, A = 6, Z = 0, R = 1, C = 2, N = -2, O = -3, D = -4, I = -5, U = 8, T = 1, F = 2, L = 3, H = 4, j = 5, K = 6, M = 7, P = 8, Y = 9, q = 10, G = 11, X = 12, W = 13, J = 14, Q = 15, V = 16, $ = 17, tt = 18, et = 19, at = 20, it = 21, nt = 22, rt = 23, st = 24, ot = 25, lt = 26, ht = 27, dt = 28, ft = 29, _t = 30, ut = 31, ct = 32, bt = 852, gt = 592, mt = 15, wt = mt, pt = !0; a.inflateReset = s, a.inflateReset2 = o, a.inflateResetKeep = r, a.inflateInit = h, a.inflateInit2 = l, a.inflate = _, a.inflateEnd = u, a.inflateGetHeader = c, a.inflateSetDictionary = b, a.inflateInfo = "pako inflate (from Nodeca project)"
        }, { "../utils/common": 3, "./adler32": 5, "./crc32": 7, "./inffast": 10, "./inftrees": 12 }], 12: [function (t, e, a) { "use strict"; var i = t("../utils/common"), n = 15, r = 852, s = 592, o = 0, l = 1, h = 2, d = [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258, 0, 0], f = [16, 16, 16, 16, 16, 16, 16, 16, 17, 17, 17, 17, 18, 18, 18, 18, 19, 19, 19, 19, 20, 20, 20, 20, 21, 21, 21, 21, 16, 72, 78], _ = [1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577, 0, 0], u = [16, 16, 16, 16, 17, 17, 18, 18, 19, 19, 20, 20, 21, 21, 22, 22, 23, 23, 24, 24, 25, 25, 26, 26, 27, 27, 28, 28, 29, 29, 64, 64]; e.exports = function (t, e, a, c, b, g, m, w) { var p, v, k, y, x, z, B, S, E, A = w.bits, Z = 0, R = 0, C = 0, N = 0, O = 0, D = 0, I = 0, U = 0, T = 0, F = 0, L = null, H = 0, j = new i.Buf16(n + 1), K = new i.Buf16(n + 1), M = null, P = 0; for (Z = 0; Z <= n; Z++)j[Z] = 0; for (R = 0; R < c; R++)j[e[a + R]]++; for (O = A, N = n; N >= 1 && 0 === j[N]; N--); if (O > N && (O = N), 0 === N) return b[g++] = 20971520, b[g++] = 20971520, w.bits = 1, 0; for (C = 1; C < N && 0 === j[C]; C++); for (O < C && (O = C), U = 1, Z = 1; Z <= n; Z++)if (U <<= 1, U -= j[Z], U < 0) return -1; if (U > 0 && (t === o || 1 !== N)) return -1; for (K[1] = 0, Z = 1; Z < n; Z++)K[Z + 1] = K[Z] + j[Z]; for (R = 0; R < c; R++)0 !== e[a + R] && (m[K[e[a + R]]++] = R); if (t === o ? (L = M = m, z = 19) : t === l ? (L = d, H -= 257, M = f, P -= 257, z = 256) : (L = _, M = u, z = -1), F = 0, R = 0, Z = C, x = g, D = O, I = 0, k = -1, T = 1 << O, y = T - 1, t === l && T > r || t === h && T > s) return 1; for (var Y = 0; ;) { Y++ , B = Z - I, m[R] < z ? (S = 0, E = m[R]) : m[R] > z ? (S = M[P + m[R]], E = L[H + m[R]]) : (S = 96, E = 0), p = 1 << Z - I, v = 1 << D, C = v; do v -= p, b[x + (F >> I) + v] = B << 24 | S << 16 | E | 0; while (0 !== v); for (p = 1 << Z - 1; F & p;)p >>= 1; if (0 !== p ? (F &= p - 1, F += p) : F = 0, R++ , 0 === --j[Z]) { if (Z === N) break; Z = e[a + m[R]] } if (Z > O && (F & y) !== k) { for (0 === I && (I = O), x += C, D = Z - I, U = 1 << D; D + I < N && (U -= j[D + I], !(U <= 0));)D++ , U <<= 1; if (T += 1 << D, t === l && T > r || t === h && T > s) return 1; k = F & y, b[k] = O << 24 | D << 16 | x - g | 0 } } return 0 !== F && (b[x + F] = Z - I << 24 | 64 << 16 | 0), w.bits = O, 0 } }, { "../utils/common": 3 }], 13: [function (t, e, a) { "use strict"; e.exports = { 2: "need dictionary", 1: "stream end", 0: "", "-1": "file error", "-2": "stream error", "-3": "data error", "-4": "insufficient memory", "-5": "buffer error", "-6": "incompatible version" } }, {}], 14: [function (t, e, a) { "use strict"; function i(t) { for (var e = t.length; --e >= 0;)t[e] = 0 } function n(t, e, a, i, n) { this.static_tree = t, this.extra_bits = e, this.extra_base = a, this.elems = i, this.max_length = n, this.has_stree = t && t.length } function r(t, e) { this.dyn_tree = t, this.max_code = 0, this.stat_desc = e } function s(t) { return t < 256 ? lt[t] : lt[256 + (t >>> 7)] } function o(t, e) { t.pending_buf[t.pending++] = 255 & e, t.pending_buf[t.pending++] = e >>> 8 & 255 } function l(t, e, a) { t.bi_valid > W - a ? (t.bi_buf |= e << t.bi_valid & 65535, o(t, t.bi_buf), t.bi_buf = e >> W - t.bi_valid, t.bi_valid += a - W) : (t.bi_buf |= e << t.bi_valid & 65535, t.bi_valid += a) } function h(t, e, a) { l(t, a[2 * e], a[2 * e + 1]) } function d(t, e) { var a = 0; do a |= 1 & t, t >>>= 1, a <<= 1; while (--e > 0); return a >>> 1 } function f(t) { 16 === t.bi_valid ? (o(t, t.bi_buf), t.bi_buf = 0, t.bi_valid = 0) : t.bi_valid >= 8 && (t.pending_buf[t.pending++] = 255 & t.bi_buf, t.bi_buf >>= 8, t.bi_valid -= 8) } function _(t, e) { var a, i, n, r, s, o, l = e.dyn_tree, h = e.max_code, d = e.stat_desc.static_tree, f = e.stat_desc.has_stree, _ = e.stat_desc.extra_bits, u = e.stat_desc.extra_base, c = e.stat_desc.max_length, b = 0; for (r = 0; r <= X; r++)t.bl_count[r] = 0; for (l[2 * t.heap[t.heap_max] + 1] = 0, a = t.heap_max + 1; a < G; a++)i = t.heap[a], r = l[2 * l[2 * i + 1] + 1] + 1, r > c && (r = c, b++), l[2 * i + 1] = r, i > h || (t.bl_count[r]++ , s = 0, i >= u && (s = _[i - u]), o = l[2 * i], t.opt_len += o * (r + s), f && (t.static_len += o * (d[2 * i + 1] + s))); if (0 !== b) { do { for (r = c - 1; 0 === t.bl_count[r];)r--; t.bl_count[r]-- , t.bl_count[r + 1] += 2, t.bl_count[c]-- , b -= 2 } while (b > 0); for (r = c; 0 !== r; r--)for (i = t.bl_count[r]; 0 !== i;)n = t.heap[--a], n > h || (l[2 * n + 1] !== r && (t.opt_len += (r - l[2 * n + 1]) * l[2 * n], l[2 * n + 1] = r), i--) } } function u(t, e, a) { var i, n, r = new Array(X + 1), s = 0; for (i = 1; i <= X; i++)r[i] = s = s + a[i - 1] << 1; for (n = 0; n <= e; n++) { var o = t[2 * n + 1]; 0 !== o && (t[2 * n] = d(r[o]++, o)) } } function c() { var t, e, a, i, r, s = new Array(X + 1); for (a = 0, i = 0; i < K - 1; i++)for (dt[i] = a, t = 0; t < 1 << et[i]; t++)ht[a++] = i; for (ht[a - 1] = i, r = 0, i = 0; i < 16; i++)for (ft[i] = r, t = 0; t < 1 << at[i]; t++)lt[r++] = i; for (r >>= 7; i < Y; i++)for (ft[i] = r << 7, t = 0; t < 1 << at[i] - 7; t++)lt[256 + r++] = i; for (e = 0; e <= X; e++)s[e] = 0; for (t = 0; t <= 143;)st[2 * t + 1] = 8, t++ , s[8]++; for (; t <= 255;)st[2 * t + 1] = 9, t++ , s[9]++; for (; t <= 279;)st[2 * t + 1] = 7, t++ , s[7]++; for (; t <= 287;)st[2 * t + 1] = 8, t++ , s[8]++; for (u(st, P + 1, s), t = 0; t < Y; t++)ot[2 * t + 1] = 5, ot[2 * t] = d(t, 5); _t = new n(st, et, M + 1, P, X), ut = new n(ot, at, 0, Y, X), ct = new n(new Array(0), it, 0, q, J) } function b(t) { var e; for (e = 0; e < P; e++)t.dyn_ltree[2 * e] = 0; for (e = 0; e < Y; e++)t.dyn_dtree[2 * e] = 0; for (e = 0; e < q; e++)t.bl_tree[2 * e] = 0; t.dyn_ltree[2 * Q] = 1, t.opt_len = t.static_len = 0, t.last_lit = t.matches = 0 } function g(t) { t.bi_valid > 8 ? o(t, t.bi_buf) : t.bi_valid > 0 && (t.pending_buf[t.pending++] = t.bi_buf), t.bi_buf = 0, t.bi_valid = 0 } function m(t, e, a, i) { g(t), i && (o(t, a), o(t, ~a)), N.arraySet(t.pending_buf, t.window, e, a, t.pending), t.pending += a } function w(t, e, a, i) { var n = 2 * e, r = 2 * a; return t[n] < t[r] || t[n] === t[r] && i[e] <= i[a] } function p(t, e, a) { for (var i = t.heap[a], n = a << 1; n <= t.heap_len && (n < t.heap_len && w(e, t.heap[n + 1], t.heap[n], t.depth) && n++ , !w(e, i, t.heap[n], t.depth));)t.heap[a] = t.heap[n], a = n, n <<= 1; t.heap[a] = i } function v(t, e, a) { var i, n, r, o, d = 0; if (0 !== t.last_lit) do i = t.pending_buf[t.d_buf + 2 * d] << 8 | t.pending_buf[t.d_buf + 2 * d + 1], n = t.pending_buf[t.l_buf + d], d++ , 0 === i ? h(t, n, e) : (r = ht[n], h(t, r + M + 1, e), o = et[r], 0 !== o && (n -= dt[r], l(t, n, o)), i-- , r = s(i), h(t, r, a), o = at[r], 0 !== o && (i -= ft[r], l(t, i, o))); while (d < t.last_lit); h(t, Q, e) } function k(t, e) { var a, i, n, r = e.dyn_tree, s = e.stat_desc.static_tree, o = e.stat_desc.has_stree, l = e.stat_desc.elems, h = -1; for (t.heap_len = 0, t.heap_max = G, a = 0; a < l; a++)0 !== r[2 * a] ? (t.heap[++t.heap_len] = h = a, t.depth[a] = 0) : r[2 * a + 1] = 0; for (; t.heap_len < 2;)n = t.heap[++t.heap_len] = h < 2 ? ++h : 0, r[2 * n] = 1, t.depth[n] = 0, t.opt_len-- , o && (t.static_len -= s[2 * n + 1]); for (e.max_code = h, a = t.heap_len >> 1; a >= 1; a--)p(t, r, a); n = l; do a = t.heap[1], t.heap[1] = t.heap[t.heap_len--], p(t, r, 1), i = t.heap[1], t.heap[--t.heap_max] = a, t.heap[--t.heap_max] = i, r[2 * n] = r[2 * a] + r[2 * i], t.depth[n] = (t.depth[a] >= t.depth[i] ? t.depth[a] : t.depth[i]) + 1, r[2 * a + 1] = r[2 * i + 1] = n, t.heap[1] = n++ , p(t, r, 1); while (t.heap_len >= 2); t.heap[--t.heap_max] = t.heap[1], _(t, e), u(r, h, t.bl_count) } function y(t, e, a) { var i, n, r = -1, s = e[1], o = 0, l = 7, h = 4; for (0 === s && (l = 138, h = 3), e[2 * (a + 1) + 1] = 65535, i = 0; i <= a; i++)n = s, s = e[2 * (i + 1) + 1], ++o < l && n === s || (o < h ? t.bl_tree[2 * n] += o : 0 !== n ? (n !== r && t.bl_tree[2 * n]++ , t.bl_tree[2 * V]++) : o <= 10 ? t.bl_tree[2 * $]++ : t.bl_tree[2 * tt]++ , o = 0, r = n, 0 === s ? (l = 138, h = 3) : n === s ? (l = 6, h = 3) : (l = 7, h = 4)) } function x(t, e, a) { var i, n, r = -1, s = e[1], o = 0, d = 7, f = 4; for (0 === s && (d = 138, f = 3), i = 0; i <= a; i++)if (n = s, s = e[2 * (i + 1) + 1], !(++o < d && n === s)) { if (o < f) { do h(t, n, t.bl_tree); while (0 !== --o) } else 0 !== n ? (n !== r && (h(t, n, t.bl_tree), o--), h(t, V, t.bl_tree), l(t, o - 3, 2)) : o <= 10 ? (h(t, $, t.bl_tree), l(t, o - 3, 3)) : (h(t, tt, t.bl_tree), l(t, o - 11, 7)); o = 0, r = n, 0 === s ? (d = 138, f = 3) : n === s ? (d = 6, f = 3) : (d = 7, f = 4) } } function z(t) { var e; for (y(t, t.dyn_ltree, t.l_desc.max_code), y(t, t.dyn_dtree, t.d_desc.max_code), k(t, t.bl_desc), e = q - 1; e >= 3 && 0 === t.bl_tree[2 * nt[e] + 1]; e--); return t.opt_len += 3 * (e + 1) + 5 + 5 + 4, e } function B(t, e, a, i) { var n; for (l(t, e - 257, 5), l(t, a - 1, 5), l(t, i - 4, 4), n = 0; n < i; n++)l(t, t.bl_tree[2 * nt[n] + 1], 3); x(t, t.dyn_ltree, e - 1), x(t, t.dyn_dtree, a - 1) } function S(t) { var e, a = 4093624447; for (e = 0; e <= 31; e++ , a >>>= 1)if (1 & a && 0 !== t.dyn_ltree[2 * e]) return D; if (0 !== t.dyn_ltree[18] || 0 !== t.dyn_ltree[20] || 0 !== t.dyn_ltree[26]) return I; for (e = 32; e < M; e++)if (0 !== t.dyn_ltree[2 * e]) return I; return D } function E(t) { bt || (c(), bt = !0), t.l_desc = new r(t.dyn_ltree, _t), t.d_desc = new r(t.dyn_dtree, ut), t.bl_desc = new r(t.bl_tree, ct), t.bi_buf = 0, t.bi_valid = 0, b(t) } function A(t, e, a, i) { l(t, (T << 1) + (i ? 1 : 0), 3), m(t, e, a, !0) } function Z(t) { l(t, F << 1, 3), h(t, Q, st), f(t) } function R(t, e, a, i) { var n, r, s = 0; t.level > 0 ? (t.strm.data_type === U && (t.strm.data_type = S(t)), k(t, t.l_desc), k(t, t.d_desc), s = z(t), n = t.opt_len + 3 + 7 >>> 3, r = t.static_len + 3 + 7 >>> 3, r <= n && (n = r)) : n = r = a + 5, a + 4 <= n && e !== -1 ? A(t, e, a, i) : t.strategy === O || r === n ? (l(t, (F << 1) + (i ? 1 : 0), 3), v(t, st, ot)) : (l(t, (L << 1) + (i ? 1 : 0), 3), B(t, t.l_desc.max_code + 1, t.d_desc.max_code + 1, s + 1), v(t, t.dyn_ltree, t.dyn_dtree)), b(t), i && g(t) } function C(t, e, a) { return t.pending_buf[t.d_buf + 2 * t.last_lit] = e >>> 8 & 255, t.pending_buf[t.d_buf + 2 * t.last_lit + 1] = 255 & e, t.pending_buf[t.l_buf + t.last_lit] = 255 & a, t.last_lit++ , 0 === e ? t.dyn_ltree[2 * a]++ : (t.matches++ , e-- , t.dyn_ltree[2 * (ht[a] + M + 1)]++ , t.dyn_dtree[2 * s(e)]++), t.last_lit === t.lit_bufsize - 1 } var N = t("../utils/common"), O = 4, D = 0, I = 1, U = 2, T = 0, F = 1, L = 2, H = 3, j = 258, K = 29, M = 256, P = M + 1 + K, Y = 30, q = 19, G = 2 * P + 1, X = 15, W = 16, J = 7, Q = 256, V = 16, $ = 17, tt = 18, et = [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0], at = [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13], it = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 3, 7], nt = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15], rt = 512, st = new Array(2 * (P + 2)); i(st); var ot = new Array(2 * Y); i(ot); var lt = new Array(rt); i(lt); var ht = new Array(j - H + 1); i(ht); var dt = new Array(K); i(dt); var ft = new Array(Y); i(ft); var _t, ut, ct, bt = !1; a._tr_init = E, a._tr_stored_block = A, a._tr_flush_block = R, a._tr_tally = C, a._tr_align = Z }, { "../utils/common": 3 }], 15: [function (t, e, a) { "use strict"; function i() { this.input = null, this.next_in = 0, this.avail_in = 0, this.total_in = 0, this.output = null, this.next_out = 0, this.avail_out = 0, this.total_out = 0, this.msg = "", this.state = null, this.data_type = 2, this.adler = 0 } e.exports = i }, {}], "/": [function (t, e, a) { "use strict"; var i = t("./lib/utils/common").assign, n = t("./lib/deflate"), r = t("./lib/inflate"), s = t("./lib/zlib/constants"), o = {}; i(o, n, r, s), e.exports = o }, { "./lib/deflate": 1, "./lib/inflate": 2, "./lib/utils/common": 3, "./lib/zlib/constants": 6 }]
    }, {}, [])("/")
});

