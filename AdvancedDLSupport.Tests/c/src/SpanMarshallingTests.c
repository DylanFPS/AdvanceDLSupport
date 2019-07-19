﻿#include "comp.h"
#include "TestStruct.h"

__declspec(dllexport) int32_t* GetInt32ArrayZeroToNine()
{
	int32_t* arr = malloc(sizeof(int32_t) * 10);

	for (int i = 0; i < 10; i++)
		arr[i] = i;

	return arr;
}

__declspec(dllexport) int32_t IsInt32ArrayZeroToNine(int32_t* arr)
{
	int32_t ret = 1;

	for (int i = 0; i < 10; i++)
	{
	    if (arr[i] != i)
	    {
	        ret = 0;
	    }
	}

	return ret;
}