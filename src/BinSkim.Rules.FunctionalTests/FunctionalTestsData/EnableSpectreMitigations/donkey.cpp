#include <stdio.h>

void Donkey()
{
    printf ("Donkey!\n");
}

#ifdef TESTDISABLESPECTREBYPRAGMA
#pragma optimize("g", off)
#endif
void WoofBark()
{
    printf("Woof-Bark!\n");
}
#ifdef TESTDISABLESPECTREBYPRAGMA
#pragma optimize("g", on)
#endif

#ifdef TESTDISABLESPECTREBYDECLSPEC
__declspec(spectre(nomitigation))
#endif
void Eeyore()
{
    printf("Eeyore!\n");
}

__declspec(dllexport)
void PrintMe()
{
    Donkey();
    WoofBark();
    Eeyore();
}

