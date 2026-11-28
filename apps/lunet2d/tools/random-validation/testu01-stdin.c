#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include "unif01.h"
#include "bbattery.h"

static unsigned int bits(void)
{
    static uint32_t block[4096];
    static size_t count, index;
    if (index == count) {
        count = fread(block, sizeof(*block), sizeof(block) / sizeof(*block), stdin);
        if (count == 0) exit(2);
        index = 0;
    }
    return block[index++];
}

int main(int argc, char **argv)
{
    unif01_Gen *gen = unif01_CreateExternGenBits("Lunet Xoshiro256** (C# output)", bits);
    if (argc > 1 && strcmp(argv[1], "bigcrush") == 0) bbattery_BigCrush(gen);
    else if (argc > 1 && strcmp(argv[1], "crush") == 0) bbattery_Crush(gen);
    else bbattery_SmallCrush(gen);
    unif01_DeleteExternGenBits(gen);
}
