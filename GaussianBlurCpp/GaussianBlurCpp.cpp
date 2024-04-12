/*
New version of our c++ implementation of the Gaussian Blur in which we apply the blur
firstly for all pixels in red input array, than green, then blue. 
This version's logic is the same as in assembly implementation.
*/
/*
#include "pch.h" 
#include <iostream>
#include "GaussianBlurCpp.h"

void CppGaussianBlur(int arraysize, int width, unsigned short* in_redAddr,
    unsigned short* in_greenAddr, unsigned short* in_blueAddr, unsigned short* out_redAddr,
    unsigned short* out_greenAddr, unsigned short* out_blueAddr)
{
    int kernel[9] = { 1, 2, 1, 2, 4, 2, 1, 2, 1 };
    int kernelSum = 16; // Sum of kernel values

    // Function to apply the kernel to a color channel
    auto applyKernel = [&](unsigned short* channel, unsigned short* out_channel) {
        int j = 0;
        for (int i = 0; i < arraysize; ++i)
        {
            if (j % width == 0)
            {
                j += 2;
                --i;
                continue;
            }

            int temp = 0;
            int m = 0;
            for (int k = 0; k < width * 3; k += width) {
                for (int l = 0; l < 3; ++l) {
                    temp += channel[j + k + l - 2] * kernel[m++];
                }
            }
            out_channel[i] = temp / kernelSum;
            ++j;
        }
        };

    // Apply the kernel to each color channel separately
    applyKernel(in_redAddr, out_redAddr);
    applyKernel(in_greenAddr, out_greenAddr);
    applyKernel(in_blueAddr, out_blueAddr);
}


Code below is the first version of our implementation of Gaussian Blur in c++
in this version we applied the blur for 3 colour channels for the first pixel, 
than for the second and so on.
*/
#include "pch.h"
#include <iostream>
#include "GaussianBlurCpp.h"

void CppGaussianBlur(int arraysize, int width, unsigned short* in_redAddr,
    unsigned short* in_greenAddr, unsigned short* in_blueAddr, unsigned short* out_redAddr,
    unsigned short* out_greenAddr, unsigned short* out_blueAddr)
{
    int kernel[9] = { 1, 2, 1, 2, 4, 2, 1, 2, 1 };
    int kernelSum = 16; // Sum of kernel values
    int j = 0;

    for (int i = 0; i < arraysize; ++i)
    {
        if (j % width == 0)
        {
            j += 2;
            --i;
            continue;
        }

        // Function to apply the kernel to a color channel
        auto applyKernel = [&](unsigned short* channel) {
            int temp = 0;
            int m = 0;
            for (int k = 0; k < width * 3; k += width) {
                for (int l = 0; l < 3; ++l) {
                    temp += channel[j + k + l - 2] * kernel[m++];
                }
            }
            return temp / kernelSum;
            };

        // Apply the kernel to each color channel
        out_redAddr[i] = applyKernel(in_redAddr);
        out_greenAddr[i] = applyKernel(in_greenAddr);
        out_blueAddr[i] = applyKernel(in_blueAddr);

        ++j;
    }
}