#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XerahS.Common
{
    /// <summary>
    /// Checks the Linux FFmpeg releases on ShareX/FFmpeg: SHA256SUMS must carry a valid
    /// ECDSA P-256 signature (SHA256SUMS.sig) from a pinned key, and the zip must match its line.
    /// </summary>
    public static class FFmpegReleaseVerifier
    {
        /// <summary>
        /// Public keys that may sign SHA256SUMS. Same file as ShareX/FFmpeg keys/linux-release-2026.pub.pem.
        /// To rotate, add the new key here first and keep the old one while its releases are in use.
        /// </summary>
        public static readonly IReadOnlyList<string> TrustedKeys =
        [
            """
            -----BEGIN PUBLIC KEY-----
            MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEtLBu32RqxWLpS2DoG4YD8i+LkAf4
            ga3I3An27cmwmQjTOW0Vx6TbK307E5QsdDIQ7y6Ddqe5JHmVbSlficp+ng==
            -----END PUBLIC KEY-----
            """,
        ];

        public const string ChecksumsFileName = "SHA256SUMS";
        public const string SignatureFileName = "SHA256SUMS.sig";

        private static readonly Regex ChecksumLine = new(
            @"^(?<hash>[0-9a-fA-F]{64}) [ *](?<name>\S+)$",
            RegexOptions.CultureInvariant);

        /// <summary>True when <paramref name="signature"/> (DER, as openssl dgst -sign writes it) signs <paramref name="checksums"/>.</summary>
        public static bool IsSignatureValid(byte[] checksums, byte[] signature, IEnumerable<string>? trustedKeys = null)
        {
            foreach (string pem in trustedKeys ?? TrustedKeys)
            {
                using ECDsa key = ECDsa.Create();
                key.ImportFromPem(pem);
                if (key.VerifyData(checksums, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Hash listed for <paramref name="fileName"/> in sha256sum output, lower case, or null.</summary>
        public static string? FindHash(byte[] checksums, string fileName)
        {
            foreach (string line in Encoding.UTF8.GetString(checksums).Split('\n'))
            {
                Match match = ChecksumLine.Match(line.TrimEnd('\r'));
                if (match.Success && match.Groups["name"].Value == fileName)
                {
                    return match.Groups["hash"].Value.ToLowerInvariant();
                }
            }

            return null;
        }

        public static async Task<bool> IsFileHashValidAsync(string path, string expectedHash, CancellationToken cancellationToken = default)
        {
            await using FileStream stream = File.OpenRead(path);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return string.Equals(Convert.ToHexStringLower(hash), expectedHash, StringComparison.Ordinal);
        }
    }
}
