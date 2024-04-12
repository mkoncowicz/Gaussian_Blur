.data
    arraysize DQ 0
    iterator DQ 0
    iterator_input DQ 0
    width2 DQ 0
    border_detector DQ 0

    kernel:
        DW 1, 1, 2, 4, 2, 2, 2, 1, 1
        ; our kerenl is:
        ; 1, 2, 1,
        ; 2, 4, 2,
        ; 1, 2, 1
.code

;RCX -> arraysize
;RDI -> width
;R8 -> red_input
;R9 -> green_input
;RSP+40 -> blue_input
;RSP+48 -> red_output
;RSP+56 -> green_output
;RSP+64 -> blue_output

;R13 -> width but in bytes, so RDI*2
;R14 -> current color input ptr
;R12 -> current color output ptr


AsmGaussianBlur proc
;==================================clearing iterators=====================================================
    mov [iterator], 0
    mov [iterator_input], 0
    mov [arraysize], 0
    mov [width2], 0
;=========================================================================================================

    mov [arraysize], RCX                    ; allocate size of the picture(in pixels) into arraysize

    mov rax, 2                              ; allocate constant 2 in rax
    mul rdi                                 ; multiply width * 2 
    mov R13, rax                            ; allocate width of picture in bytes into R13

    mov [width2], R13                       ; allocate width of picture in bytes into width2
    add [width2], R13                       ; width2 = 2* width in bytes

    mov [border_detector], R13              ; allocate width of picture in bytes into border_detection
    sub [border_detector], 4                ; reduct numebr of bytes by 4 / by 2 in pixels

    mov R14, R8                             ; move red input ptr into R14

    mov R12, qword ptr[RSP+48]              ; move red output ptr into R12

    vmovups ymm8, ymmword ptr [kernel]      ; load kernal weights into ymm8
    mov R11, [width2]                       ; allocate double of width to R11 

    pxor xmm1, xmm1                         ; logical exclusive XOR - zeroing xmm1
RLoop:

    movd xmm1, dword ptr[R14]               ; move two pixels (32-bit) from input array (R14 points to current color input pixel) into lower 32 bits of xmm1
    INSERTPS xmm2, xmm1, 00000000b          ; insert two pixels from xmm1 into xmm2
    PSLLDQ xmm2, 14                         ; shift xmm2 left (by 14 bytes) so that only first pixel left
    PSRLDQ xmm2, 14                         ; shift xmm2 right to get into initial position
                                            ; this pixel is the only pixel in xmm2, it should be multiplied by 1 (kernel) so it will be added at the end

    movd xmm1, dword ptr[R14+2]             ; move pixels 2 and 3 from 1st row into xmm1
    INSERTPS xmm0, xmm1, 00000000b          ; insert two pixels from xmm1 into xmm0 (without lossing rest of xmm0)
    PSLLDQ xmm0, 4                          ; shift xmm0 left by two pixels (4 bytes)

    movd xmm1, dword ptr[R14+R13]           ; move 2 pixels (1st and 2nd) from 2nd row into xmm1
    INSERTPS xmm0, xmm1, 00000000b          ; insert 2 pixels from xmm1 into xmm0 (the rest of xmm0 remains)
    PSLLDQ xmm0, 4                          ; shift xmm0 left by two pixels (4 bytes)

    movd xmm1, dword ptr[R14+(R13)+4]       ; move next pixels into xmm1 (3rd and 4th from 2nd row)
    PSLLDQ xmm1, 14                         ; shift left 14 bytes so that only 3rd pixel left 
    PSRLDQ xmm1, 12                         ; shift right (only 3rd pixel)
    INSERTPS xmm0, xmm1, 00000000b          ; insert 3rd pixel into xmm0
    PSLLDQ xmm0, 2                          ; shift left only by 1 pixel (because we inserted only 1 new pixel)

    movd xmm1, dword ptr[R14+(R11)]         ; move 2 pixels (1st and 2nd) from 3rd row into xmm1
    INSERTPS xmm0, xmm1, 00000000b          ; move 2 pixels from xmm1 into xmm0
    PSLLDQ xmm0, 2                          ; shift left only by 1 pixel as only 1 pixel is left to insert

    movd xmm1, dword ptr[R14+(R11)+4]       ; moving 2 pixels (3rd and 4th) from 3rd row into xmm1
    PSLLDQ xmm1, 14                         ; shift left 14 bytes so that only 3rd pixel left 
    PSRLDQ xmm1, 14                         ; shift right to initial position (only 3rd pixel)
    POR xmm0, xmm1                          ; logic OR on xmm0 and xmm1 so that only one pixel is inserted

    vpmullw xmm4, xmm0, xmm8                ; multiply 8 pixels from xmm0 by xmm8 (contains kernel weights), load result to xmm4

    phaddw xmm4, xmm4                       ; horizontal addition of each pair of multiplication results (4 additions)
    phaddw xmm4, xmm4                       ; horizontal addition of results (2 additions)
    phaddw xmm4, xmm4                       ; horizontal addition of results (1 addition)

    PADDW xmm2, xmm4                        ; add weightes sum to the 1st pixel left
   
    PSRAW xmm2, 4                           ; arithmetic shift right to divide result by 16 (kernel sum)

    mov R10, qword ptr[iterator]            ; move output pixel iterator into R10 to use it in addressing
    PEXTRW word ptr[R12+R10], xmm2, 0b      ; extract result from xmm2 into output array at address pointed by R12 with offset R10 (iterator)

    add iterator, 2                         ; updete iterators and the input pointer (R14) for the next pixel 
    add iterator_input, 2                   ; image iterator update
    add R14, 2

    mov RAX, border_detector                ; allocate border_detector value to RAX 
    CMP RAX, [iterator_input]               ; comarison of image iterator and border_detector value

    jnz NoPixelSkipR                        ; if values not equal, Z flag = 0, jump (if equal we are at the border)
    add R14, 4                              ; update input array iterator again
    add iterator_input, 4                   ; update image iterator again
    add [border_detector], R13              ; add width to border_detector to use in next loop

    NoPixelSkipR:

    dec arraysize                           ; decrement array size
    jnz  RLoop
    ;===========================================GREEN PREPARATION====================================================================

    mov [iterator], 0
    mov [iterator_input], 0
    mov [arraysize], 0
    mov [width2], 0

    mov [border_detector], R13
    sub [border_detector], 4

    mov [arraysize], RCX                    

    mov rax, 2
    mul rdi
    mov R13, rax

    mov [width2], R13
    add [width2], R13

    mov R14, R9

    mov R12, qword ptr[RSP+56]
    vmovups ymm8, ymmword ptr [kernel]     

    pxor xmm1,xmm1
    ;==============================================GREEN=================================================================================

    GLoop:

    movd xmm1, dword ptr[R14]           
    INSERTPS xmm2, xmm1, 00000000b     
    PSLLDQ xmm2, 14
    PSRLDQ xmm2, 14                    

    movd xmm1, dword ptr[R14+2]        
    INSERTPS xmm0, xmm1, 00000000b      
    PSLLDQ xmm0, 4                     

    movd xmm1, dword ptr[R14+R13]
    INSERTPS xmm0, xmm1, 00000000b
    PSLLDQ xmm0, 4

    movd xmm1, dword ptr[R14+(R13)+4]
    PSLLDQ xmm1, 14
    PSRLDQ xmm1, 12
    INSERTPS xmm0, xmm1, 00000000b
    PSLLDQ xmm0, 2

    movd xmm1, dword ptr[R14+(R11)]
    INSERTPS xmm0, xmm1, 00000000b
    PSLLDQ xmm0, 2

    movd xmm1, dword ptr[R14+(R11)+4]   
    PSLLDQ xmm1, 14
    PSRLDQ xmm1, 14
    POR xmm0, xmm1                      

    vpmullw xmm4, xmm0, xmm8

    phaddw xmm4,xmm4            
    phaddw xmm4,xmm4             
    phaddw xmm4,xmm4           

    PADDW xmm2, xmm4

    PSRAW xmm2, 4

    mov R10, qword ptr[iterator]
    PEXTRW word ptr[R12+R10], xmm2, 0b

    add iterator, 2
    add iterator_input, 2
    add R14, 2

    mov RAX, border_detector
    CMP RAX, [iterator_input]

    jnz NoPixelSkipG
    add R14, 4
    add iterator_input, 4
    add [border_detector], R13

    NoPixelSkipG:

    dec arraysize
    jnz  GLoop

;================================BLUE PREPARATION====================================================================================
    mov [iterator], 0
    mov [iterator_input], 0
    mov [arraysize], 0
    mov [width2], 0

    mov [border_detector], R13
    sub [border_detector], 4

    mov [arraysize], RCX            

    mov rax, 2
    mul rdi
    mov R13, rax

    mov [width2], R13
    add [width2], R13

    mov R14, qword ptr[RSP+40]

    mov R12, qword ptr[RSP+64]
    vmovups ymm8, ymmword ptr [kernel]      

    pxor xmm1,xmm1
;============================================BLUE====================================================================================
    BLoop:
   
    movd xmm1, dword ptr[R14]           
    INSERTPS xmm2, xmm1, 00000000b      
    PSLLDQ xmm2, 14
    PSRLDQ xmm2, 14                    

    movd xmm1, dword ptr[R14+2]         
    INSERTPS xmm0, xmm1, 00000000b      
    PSLLDQ xmm0, 4                      

    movd xmm1, dword ptr[R14+R13]
    INSERTPS xmm0, xmm1, 00000000b
    PSLLDQ xmm0, 4

    movd xmm1, dword ptr[R14+(R13)+4]
    PSLLDQ xmm1, 14
    PSRLDQ xmm1, 12
    INSERTPS xmm0, xmm1, 00000000b
    PSLLDQ xmm0, 2

    movd xmm1, dword ptr[R14+(R11)]
    INSERTPS xmm0, xmm1, 00000000b
    PSLLDQ xmm0, 2

    movd xmm1, dword ptr[R14+(R11)+4]  
    PSLLDQ xmm1, 14
    PSRLDQ xmm1, 14
    POR xmm0,xmm1                      

    vpmullw xmm4,xmm0,xmm8

    phaddw xmm4,xmm4           
    phaddw xmm4,xmm4            
    phaddw xmm4,xmm4            

    PADDW xmm2, xmm4

    PSRAW xmm2, 4

    mov R10, qword ptr[iterator]
    PEXTRW word ptr[R12+R10], xmm2, 0b

    add iterator, 2
    add iterator_input, 2
    add R14, 2

    mov RAX,border_detector
    CMP RAX, [iterator_input]

    jnz NoPixelSkipB
    add R14, 4
    add iterator_input, 4
    add [border_detector], R13

    NoPixelSkipB:

    dec arraysize
    jnz  BLoop

    ret
AsmGaussianBlur endp
end