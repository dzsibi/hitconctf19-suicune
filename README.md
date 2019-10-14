# Apparently it's not efficient enough

## Introduction

This is a write-up on the HITCON CTF 2019 challenge  `Suicune`. We were provided with a x86_64 ELF executable that implemented an encryption algorithm of some sort:

```
dzsibi@OUROBOROS:~$ ./suicune
Usage: ./suicune <flag> <key>
dzsibi@OUROBOROS:~$ ./suicune test 128
6edb96c1
```

The key was a simple integer, while the output was in hexadecimal format. Execution time increased exponentially as the plaintext value got bigger. A file with the expected output of the algorithm when it was applied to the real flag was also supplied. Cursory testing revealed a few things:

* Output length matches input length
* Change a bit in the input, it changes in the output
* If you add or remove an input character, other parts of the output also change
* With input sizes over 14-16 characters it seems to run indefinitely without printing any output

Based on this, we concluded:

* A keystream is generated based on the key and it is XOR-ed with the input
* The keystream depends on the input length and the key

We also knew the format of the flag: `hitcon{flag}`, with an overall length of 49 characters based on the supplied output.

## The executable

Looking at the executable it is immediately apparent that it was compiled using [Crystal](https://crystal-lang.org/), a Ruby-like language with an LLVM based compiler. Since debug symbols were present, I could immediately identify the `__crystal_main` function which was the entry point of the actual application - and which seemed to contain basically all user code, and even some framework code inlined by the compiler. After careful analysis, I was able to reverse-engineer the algorithm:

1. Parse the key as an integer and truncate it to 16 bits
2. Read the input into a byte array (the current state)
3. Initialize a PCG32 based PRNG using the truncated key
4. Perform 16 rounds of the same encryption algorithm:
   1. Fill a 256 element array with the numbers from 0 to 255
   2. Shuffle the array using Crystal's array#shuffle and the PRNG
   3. Generate a 64 bit random number using the PRNG
   4. Truncate the shuffled array to the input size
   5. Sort the array
   6. XOR the elements of the array with the input
   7. Reverse the entire array
5. After the rounds are done, print the elements as a hexadecimal string

*Sidenote: At first, I did not notice the key truncation at all, but it was called out by one of my teammates well in the middle of the next phase.*

So what was taking so long? Well, it turns out that the sorting algorithm is not exactly efficient. We can fix that!

## Reimplementation

There are only 2^16 possible keys (since the input is truncated), and the only real barrier to a brute force attack on the executable is the slow sorting algorithm - we just need to reimplement the algorithm using a faster one. Since we know the prefix of the flag, we can look for a match. After doing so, I was able to reverse example outputs, but not the actual flag output.

What was going on? I kind of ignored the 64 bit number that was generated right in the middle, since it was a huge number that was decremented one-by-one at each iteration of the sorting algorithm, and it reaching zero was used as one of the (never used) exit conditions of the loop. Surely that would never happen. Oh wait.

If the algorithm actually took more steps than the generated value, it would stop prematurely. Not exactly soon in terms of time, but still, in a state where the array was only half-sorted. This is where [@ngg](https://github.com/ngg) came to the rescue, and designed an algorithm that skips over sorting steps and decrements the counter faster than just running the original algorithm, but still stopping when the original algorithm would have stopped. Plugging in that algorithm produced the key in just a few seconds:

```
Seed: 0000B089
Flag: hitcon{nth_perm_Ruby_for_writing_X_C_for_running}
```

