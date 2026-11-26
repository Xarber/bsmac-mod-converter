// SPDX-License-Identifier: AGPL-3.0-only
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "enet.h"

// Ignorance calls the extended constructor even when the server requests plain ENet.
// The first field is the 32-bit SslMode enum. Mode 0 is explicitly plain.
ENetHost *enet_host_create_ssl(const ENetAddress *address, size_t peers, size_t channels,
                              uint32_t incoming, uint32_t outgoing, int buffer_size,
                              const void *configuration) {
    int32_t mode;
    if (!configuration) return NULL;
    memcpy(&mode, configuration, sizeof mode);
    if (mode != 0) {
        fputs("BeatTogether: this Apple Silicon transport does not implement ENet TLS.\n", stderr);
        return NULL;
    }
    return enet_host_create(address, peers, channels, incoming, outgoing, buffer_size);
}
