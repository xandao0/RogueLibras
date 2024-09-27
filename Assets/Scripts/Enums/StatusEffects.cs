using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

public enum StatusEffects
{
    NONE,
    FRAIL, //25% LESS BLOCK GAINED BY CARDS
    WEAK, //25% LESS DAMAGE DEALT BY CARDS
    VULNERABLE, //50% MORE DAMAGE RECEIVED
    MIGHT, //MODIFIES CARD DAMAGE
    COUNT
}
