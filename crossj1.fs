( Cross-compiler for the J1                  JCB 13:12 08/24/10)
decimal

( outfile is fileid or zero                  JCB 12:30 11/27/10)

0 value outfile

\ warning off
: type ( c-addr u )
    outfile if
        outfile write-file throw
    else
        type
    then
;
: emit ( u )
    outfile if
        pad c! pad 1 outfile write-file throw
    else
        emit
    then
;
: cr ( u )
    outfile if
        s" " outfile write-line throw
    else
        cr
    then
;
: space bl emit ;
: spaces dup 0> if 0 do space loop then ;
\ warning on

vocabulary j1assembler  \ assembly storage and instructions
vocabulary metacompiler \ the cross-compiling words
vocabulary j1target     \ actual target words

: j1asm
    only metacompiler
    also j1assembler definitions
    also forth ;
: meta
    only j1target
    also j1assembler
    also metacompiler definitions
    also forth ;
: target
    only metacompiler
    also j1target definitions ;

\ \\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
meta

\ Very rudimentary implementation of crosscompiler words
\ CDATA, IDATA and UDATA are ignored.
\ High address is ignored and no range checking is performed.
variable 'tdp

: section ( low-addr high-addr -- )
    create drop , does> 'tdp ! ;

: cdata ;
: udata ;
\ : idata ;

j1asm

: tcell 2 ;
: tcells tcell * ;
: tcell+ tcell + ;
$10000 allocate throw constant tflash

: tdp       'tdp @ ;
: there     tdp @ ;
: islegal   dup $7fff u> abort" illegal address" ;
: tc!       islegal tflash + c! ;
: tc@       islegal tflash + c@ ;
: t!        islegal over $ff and over tc! swap 8 rshift swap 1+ tc! ;
: t@        islegal dup tc@ swap 1+ tc@ 8 lshift or ;
: talign    tdp @ 1 + $fffe and tdp ! ;
: tc,       there tc! 1 tdp +! ;
: t,        there t! tcell tdp +! ;
: org       tdp ! ;

tflash $10000 $ff fill

$10000 cells allocate throw constant references
: referenced cells references + 1 swap +! ;

$10000 cells allocate throw constant labels
labels $10000 cells 0 fill
: atlabel? ( -- f = are we at a label )
    labels there cells + @ 0<>
;

: preserve  ( c-addr1 u -- c-addr )
    dup 1+ allocate throw dup >r
    2dup c! 1+
    swap cmove r> ;

: setlabel ( c-addr u -- )
    atlabel? if 2drop else preserve labels there cells + ! then ;

j1asm

: imm $8000 or t, ;

: T         $0000 ;
: N         $0100 ;
: T+N       $0200 ;
: T&N       $0300 ;
: T|N       $0400 ;
: T^N       $0500 ;
: ~T        $0600 ;
: N==T      $0700 ;
: N<T       $0800 ;
: N>>T      $0900 ;
: T-1       $0a00 ;
: rT        $0b00 ;
: [T]       $0c00 ;
: N<<T      $0d00 ;
: dsp       $0e00 ;
: Nu<T      $0f00 ;

: T->N      $0080 or ;
: T->R      $0040 or ;
: N->[T]    $0020 or ;
: d-1       $0003 or ;
: d+1       $0001 or ;
: r-1       $000c or ;
: r-2       $0008 or ;
: r+1       $0004 or ;

: alu       $6000 or t, ;

: return    T  $1000 or r-1 alu ;
: ubranch   2/ $0000 or t, ;
: 0branch   2/ $2000 or t, ;
: scall     2/ $4000 or t, ;

\ \ hide Swift-Forth's definition of N
\ warning off
\ also forth definitions
\ : N ( -- n ) [ j1assembler ] N ;
\ warning on

 meta

: dump-words ( c-addr n -- ) \ Write n/2 words from c-addr
    dup 6 > abort" invalid byte count"
    2/ dup >r
    0 do
        dup t@ s>d <# # # # # #> type space
        2 +
    loop drop
    3 r> - 5 * spaces
;

variable padc
: pad+ ( c-addr u -- ) \ append to pad
    dup >r
    pad padc @ + swap cmove
    r> padc +! ;

: pad+loc  ( addr -- )
    dup cells labels + @ ?dup if
        nip count pad+
    else
        s>d <# #s [char] $ hold #> pad+
    then
    s"  " pad+
;

: hex-literal ( u -- c-addr u ) s>d <# bl hold #s [char] $ hold #> ;

: disassemble-j
    0 padc !
    dup t@ $8000 and if
        s" LIT " pad+
        dup t@ $7fff and hex-literal pad+ exit
    else
        dup t@ $e000 and $6000 = if
            s" ALU " pad+
            dup t@ pad+loc exit
        else
            dup t@ $e000 and $4000 = if
                s" CALL "
            else
                dup t@ $2000 and if 
                    s" 0BRANCH "
                else
                    s" BRANCH "
                then
            then
            pad+
            dup t@ $1fff and 2* pad+loc
        then
    then
;

: disassemble-line ( offset -- offset' )
    dup cells labels + @ ?dup if s" \ " type count type cr then
    dup s>d <# # # # # #> type space 
    dup 2 dump-words
    disassemble-j
    pad padc @ type
    2 + 
    cr
;

: disassemble-block
    0 do
        disassemble-line
    loop
    drop
;

j1asm

\ tcompile is like "STATE": it is true when compiling

variable tcompile
: tcompile? tcompile @ ;
: +tcompile tcompile? abort" Already in compilation mode" 1 tcompile !  ;
: -tcompile 0 tcompile ! ;

: (lit)
    \ dup $f rshift over $e rshift xor 1 and throw
    dup $8000 and if
        $ffff xor recurse
        ~T alu
    else
        $8000 or t,
    then

;
: (t-constant)
    tcompile? if
        (lit)
    then
;

meta

\ Find name - without consuming it - and return a counted string
: wordstr ( "name" -- c-addr u )
    >in @ >r bl word count r> >in !
;


: literal (lit) ; immediate
: 2literal swap (lit) (lit) ; immediate
: call,
    dup referenced
    scall
;

: t:
    talign
    wordstr setlabel
    create
        there ,
        +tcompile 
        947947
    does>
        @
        tcompile? if
            call,
        then
;

: lookback ( offset -- v ) there swap - t@ ;
: prevcall?  2 lookback $e000 and $4000 = ;
: call>goto dup t@ $1fff and swap t! ;
: prevsafe?
    2 lookback $e000 and $6000 =    \ is an ALU
    2 lookback $004c and 0= and ;   \ does not touch RStack
: alu>return dup t@ $1000 or r-1 swap t! ;

: t; 947947 <> if abort" Unstructured" then
    true if
        atlabel? invert prevcall? and if
            there 2 - call>goto
        else
            atlabel? invert prevsafe? and if
                there 2 - alu>return
            else
                return
            then
        then
    else
        return
    then
    -tcompile
;

: t;fallthru 947947 <> if abort" Unstructured" then
    -tcompile
;

variable shadow-tcompile
wordlist constant escape]-wordlist
escape]-wordlist set-current
: ] shadow-tcompile @ tcompile ! previous previous ;

meta

: [ 
    tcompile @ shadow-tcompile !
    -tcompile get-order forth-wordlist escape]-wordlist rot 2 + set-order
;

: : t: ;
: ; t; ;
: ;fallthru t;fallthru ;
: , t, ;
: h, t, ;
: c, tc, ;
    
: constant ( n "name" -- ) create , immediate does> @ (t-constant) ;

: ]asm 
    -tcompile also forth also j1target also j1assembler ;
: asm[ +tcompile previous previous previous ;
: code t: ]asm ;

j1asm

: end-code
    947947 <> if abort" Unstructured" then
    previous previous previous ;

meta

\ Some Forth words are safe to use in target mode, so import them

: ( postpone ( ;
: \ postpone \ ;

: import ( "name" -- )
    >in @ ' swap >in !
    create , does> @ execute ;

import meta
import org
import include
import [if]
import [else]
import [then]
import char

: do-number ( n -- |n )
    state @ if
        postpone literal
    else
        tcompile? if
            (lit)
        then
    then
;

decimal

: [char] ( "name" -- ) ( run: -- ascii) char (lit) ;

: ['] ( "name" -- ) ( run: -- xt )
    ' tcompile @ >r -tcompile execute r> tcompile !
    dup referenced
    (lit)
;

: (sliteral--h) ( addr n -- ptr ) ( run: -- eeaddr n )
    s" sliteral" evaluate
    there >r
    dup tc,
    0 do count tc, loop
    drop
    talign
    r>
;

: (sliteral) (sliteral--h) drop ;
: s" ( "ccc<quote>" -- ) ( run: -- eaddr n ) [char] " parse (sliteral) ;
: s' ( "ccc<quote>" -- ) ( run: -- eaddr n ) [char] ' parse (sliteral) ;

: create
    talign
    wordstr setlabel
    create  there ,
    does>   @ do-number
;

: allot     tdp +! ;

: variable  wordstr setlabel create there , 0 t,
            does> @ do-number ;
: 2variable  wordstr setlabel create there , 0 t, 0 t,
            does> @ do-number ;

: createdoes
    wordstr setlabel
    create there , ' ,
    does> dup @ dup referenced (lit) cell+ @ execute
;

: jumptable 
    wordstr setlabel
    create there ,
    does> s" 2*" evaluate @ dup referenced (lit) s" + @" evaluate
;

: | ' execute dup referenced t, ;

: ', ' execute t, ;

( DEFER                                      JCB 11:18 11/12/10)

: defer
    wordstr setlabel
    create there , 0 t,
    does> @ tcompile? if do-number s" @ execute" evaluate then ;

: is ( xt "name" -- )
    tcompile? if
        ' >body @ do-number
        s" ! " evaluate
    else
        ' execute t!
    then ;

: ' ' execute ;

( VALUE                                      JCB 13:06 11/12/10)

: value
    wordstr setlabel
    create there , t,
    does> @ do-number s" @" evaluate ;

: to ( u "name" -- )
    ' >body @ do-number s" !" evaluate ;

( ARRAY                                      JCB 13:34 11/12/10)

: array
    wordstr setlabel
    create there , 0 do 0 t, loop
    does> s" cells" evaluate @ do-number s" +" evaluate ;
: 2array
    wordstr setlabel
    create there , 2* 0 do 0 t, loop
    does> s" 2* cells" evaluate @ do-number s" +" evaluate ;

( eforth's way of handling constants         JCB 13:12 09/03/10)

: sign>number
    over c@ [char] - = if
        1- swap 1+ swap
        >number
        2swap dnegate 2swap
    else
        >number
    then
;

: base>number ( caddr u base -- )
    base @ >r base !
    sign>number
    r> base !
    dup 0= if
        2drop drop do-number
    else
        1 = swap c@ [char] . = and if
            drop dup do-number 16 rshift do-number
        else
            -1 abort" bad number"
        then
    then ;

: d# 0. bl word count 10 base>number ;
: h# 0. bl word count 16 base>number ;

( Conditionals                               JCB 13:12 09/03/10)
: if
    there
    0 0branch
;

: resolve
    dup t@ there 2/ or swap t!
;

: then
    resolve
    s" (then)" setlabel
;

: else
    there
    0 ubranch 
    swap resolve
    s" (else)" setlabel
;


: begin s" (begin)" setlabel there ;
: again 
    ubranch
;
: until
    0branch
;
: while
    there
    0 0branch
;
: repeat
    swap ubranch
    resolve
    s" (repeat)" setlabel
;

: 0do    s" >r d# 0 >r"  evaluate there s" (do)" setlabel ; \ save one clock cycle and one word
: do     s" 2>r"         evaluate there s" (do)" setlabel ;
: loop   s" looptest"    evaluate 0branch ;
: i      s" r@"          evaluate ;

[undefined] sourceline [IF]
    77 constant sourceline#
[THEN]

[undefined] sourcefilename [IF]
    s" none" 2constant sourcefilename
[THEN]

: line# sourceline# (lit) ;
create currfilename 1 cells 80 + allot
variable currfilename#
: savestr ( c-addr u dst -- ) 2dup c! 1+ swap cmove ;
: getfilename sourcefilename currfilename count compare 0<>
    if
        sourcefilename 2dup currfilename savestr (sliteral--h) currfilename# !
    else
        currfilename# @ dup 1+ (lit) tc@ (lit)
    then ;
: snap line# getfilename s" (snap)" evaluate ; immediate
: assert 0= if line# sourcefilename (sliteral) s" (assert)" evaluate then ; immediate

\ hide Swift-Forth's definition of N
\ warning off
also forth definitions
: N ( -- n ) [ j1assembler ] N ;
\ warning on
