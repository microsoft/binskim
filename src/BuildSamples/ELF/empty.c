#include <stdio.h>
#include <string.h>

// This creates a super basic c-program that should trigger the stack protector,
// and uses a function that can be hardened.
int main(int argc, char** args){
    char x[10];
    char y[5];
    memcpy(x, y, (int)args[0][0]);
    printf("Hello world");
    return 0;
}