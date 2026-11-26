# SPDX-License-Identifier: AGPL-3.0-only
"""Normalize Darwin's IPv4 sockaddr result on dual-stack ENet sockets."""
from pathlib import Path
import sys

header = Path(sys.argv[1])
original = header.read_bytes()
newline = b"\r\n" if b"\r\n" in original else b"\n"
source = original.decode().replace("\r\n", "\n")
begin = source.index("recvLength = recvmsg(")
end = source.index("\n\treturn recvLength;", begin)
block = source[begin:end]
needle = """		address->ipv6 = sin.sin6_addr;
		address->port = ENET_NET_TO_HOST_16(sin.sin6_port);"""
replacement = """#ifdef __APPLE__
		if (sin.sin6_family == AF_INET && msgHdr.msg_namelen >= sizeof(struct sockaddr_in)) {
			struct sockaddr_in ipv4;
			memcpy(&ipv4, &sin, sizeof(ipv4));
			memset(&address->ipv6, 0, sizeof(address->ipv6));
			address->ipv4.ffff = 0xFFFF;
			address->ipv4.ip = ipv4.sin_addr;
			address->port = ENET_NET_TO_HOST_16(ipv4.sin_port);
		} else
#endif
		{
			address->ipv6 = sin.sin6_addr;
			address->port = ENET_NET_TO_HOST_16(sin.sin6_port);
		}"""
if block.count(needle) != 1:
    raise ValueError("Unexpected ENet POSIX receive implementation")
updated = source[:begin] + block.replace(needle, replacement) + source[end:]
header.write_bytes(updated.encode().replace(b"\n", newline))
