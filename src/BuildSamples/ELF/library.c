#include <stdio.h>
#include <string.h>
#include "library.h"

void my_memcpy(void* start, void* dest, size_t bytes)
{
    int array[100];
    memcpy(array, start, bytes);
    memcpy(dest, array, bytes);
}

void print_hello(){
    printf("hello");
}
