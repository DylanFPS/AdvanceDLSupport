﻿#include "comp.h"
#include "TestStruct.h"

int32_t globalArray[10];
int isInitialized = 0;

void InitGlobals()
{
	if (isInitialized == 0)
	{
		for (int i = 0; i < 10; ++i)
		{
			globalArray[i] = i;
		}

		isInitialized = 1;
	}
}

//Rewriten so there will not be a memory leak
__declspec(dllexport) int32_t* GetInt32ArrayZeroToNine()
{
	InitGlobals();

	return globalArray;
}

__declspec(dllexport) void WriteToInt32Array(int32_t* arr, int arrLen)
{
	InitGlobals();

	int len = arrLen < 10 ? arrLen : 10;

	for (int i = 0; i < len; ++i)
	{
		arr[i] = globalArray[i];
	}
}

__declspec(dllexport) void WriteToInt32Arrays(int32_t* arr, int arrLen, int arrLen2, int32_t* arr2)
{
	InitGlobals();

	int len = arrLen < 10 ? arrLen : 10;

	for (int i = 0; i < len; ++i)
	{
		arr[i] = globalArray[i];
	}
	
	for (int i = 0; i < arrLen2; ++ i)
	{
	    arr2[i] = arrLen2 - i;
	}
}