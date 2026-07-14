# Cierzo Arena — roster v1 (20 heroes)

`HeroRosterFactory` is the source of truth for this roster. Every row maps to one
stable `HeroId`, four `AbilityDefinition`s (Q/W/E/R), and a `HeroStats` block.
The menu, room selection, HUD and tooltip all consume these same definitions.

| Hero | Role | Attack / damage | Difficulty / curve | Fantasy and niche |
|---|---|---|---|---|
| Stone Aegis | Vanguard / Controller | Melee magical | Low / early | Ground-holding initiator and peel tank. |
| Rift Duelist | Duelist / Carry | Melee hybrid | Medium / mid | Tempo skirmisher for isolated fights. |
| Skyline Marksman | Carry / Controller | Ranged physical | Medium / late | Precision poke carry on open lanes. |
| Storm Warden | Mage / Controller | Ranged magical | Medium / mid | Storm burst against grouped enemies. |
| Cairn Warden | Support / Vanguard | Ranged magical | Low / mid | Defensive barrier support. |
| Tempest Arbiter | Controller / Mage | Ranged magical | High / late | Route denial and layered weather control. |
| Ember Bastion | Vanguard / Controller | Melee magical | Low / early | Fire tank, initiation and backline peel. |
| Ironroot Colossus | Vanguard / Support | Melee physical | Medium / mid | Rooted frontline and area control. |
| Frostveil Sentinel | Vanguard / Controller | Melee magical | Medium / mid | Ice entry denial and retreat protection. |
| Ashen Vow | Duelist / Vanguard | Melee hybrid | Medium / mid | Oathblade bruiser for prolonged duels. |
| Zephyr Reaver | Assassin / Duelist | Melee physical | High / early | Fast pick assassin with fragile exits. |
| Thornbound | Duelist / Carry | Melee physical | Medium / mid | Sticky split-pushing briar bruiser. |
| Cinderlash | Duelist / Carry | Melee physical | High / early | Volatile whip skirmisher and burst diver. |
| Sunspoke Ranger | Carry / Controller | Ranged physical | Medium / late | Sightline-dependent physical artillery. |
| Glaciershard | Mage / Controller | Ranged magical | Medium / late | Crystal artillery and clustered-target punishment. |
| Verdant Cantor | Support / Utility | Ranged magical | Medium / mid | Song-based utility, rotation and sustain support. |
| Umbral Sable | Assassin / Duelist | Melee hybrid | High / early | Shadow flank assassin; explosive but punishable. |
| Lumenweaver | Support / Utility | Ranged magical | Low / mid | Defensive light shielding and anti-dive peel. |
| Prism Oracle | Controller / Mage | Ranged magical | High / late | Precise arcane route denial. |
| Tidebinder | Controller / Support | Ranged magical | Medium / mid | Water zones, rotations and backline protection. |

## Base-stat sheets

Format: `health (+/level) · mana (+/level) · damage (+/level) · move speed ·
attack range · attack interval · health/mana regeneration`. Defence is not yet a
field in the shared prototype stat model; tank durability is expressed through
health, regeneration and shield abilities.

| Hero | Base stats |
|---|---|
| Stone Aegis | 680+105 · 220+18 · 42+5 · 4.7 · 2.5 · 1.35 · 3/4 |
| Rift Duelist | 540+82 · 240+20 · 57+8 · 5.9 · 2.35 · 1.05 · 2/5 |
| Skyline Marksman | 470+65 · 260+22 · 54+9 · 5.35 · 8.5 · 1.12 · 1.6/6 |
| Storm Warden | 455+60 · 330+32 · 38+6 · 5.1 · 7.5 · 1.3 · 1.4/7.5 |
| Cairn Warden | 560+88 · 310+30 · 35+4 · 5 · 6.5 · 1.38 · 2.7/7 |
| Tempest Arbiter | 490+70 · 300+34 · 43+6 · 5.25 · 7.2 · 1.27 · 1.8/7.2 |
| Ember Bastion | 710+110 · 230+19 · 45+5 · 4.65 · 2.45 · 1.38 · 3.4/4.2 |
| Ironroot Colossus | 735+112 · 205+16 · 43+5 · 4.45 · 2.5 · 1.42 · 3.7/3.6 |
| Frostveil Sentinel | 670+100 · 270+24 · 41+5 · 4.8 · 2.55 · 1.4 · 3/5.7 |
| Ashen Vow | 610+92 · 250+20 · 55+7 · 5.45 · 2.3 · 1.12 · 2.6/4.8 |
| Zephyr Reaver | 500+70 · 260+22 · 61+9 · 6.05 · 2.2 · 1 · 1.8/5.5 |
| Thornbound | 625+95 · 215+17 · 58+8 · 5.45 · 2.35 · 1.08 · 3.2/3.8 |
| Cinderlash | 525+76 · 245+21 · 63+9 · 5.8 · 2.25 · 1.02 · 2.1/5.2 |
| Sunspoke Ranger | 465+62 · 280+24 · 56+10 · 5.25 · 8.8 · 1.08 · 1.5/6.1 |
| Glaciershard | 440+58 · 345+34 · 37+6 · 5.05 · 7.8 · 1.32 · 1.3/7.8 |
| Verdant Cantor | 505+76 · 325+31 · 36+5 · 5.15 · 6.8 · 1.34 · 2.4/7.4 |
| Umbral Sable | 480+67 · 285+25 · 59+9 · 5.95 · 2.15 · 1 · 1.7/6.4 |
| Lumenweaver | 520+80 · 350+33 · 33+4 · 5.05 · 6.7 · 1.36 · 2.5/8 |
| Prism Oracle | 470+64 · 355+35 · 40+6 · 5.15 · 7.4 · 1.3 · 1.5/8.2 |
| Tidebinder | 535+82 · 320+30 · 39+5 · 5.2 · 7 · 1.31 · 2.5/7.3 |

## Ability sheets and level values

Each hero has Q/W/E at four ranks (hero levels 1/3/5/7) and R at three ranks
(hero levels 6/11/16). `AbilityDefinition.ConfigureRuntime` makes the per-rank
values explicit and consistent: **mana** `[M, M+5, M+10, M+15]`, **cooldown**
`[CD, CD-.4, CD-.8, CD-1.2]`, **effect** `[V, 1.35V, 1.70V, 2.05V]` (truncate
after rank 3 for R). Thus every base value, range, radius and duration below
fully specifies every rank; the exact generated arrays are the data read by UI
and gameplay.

| Hero | Q · W · E · R (base effect; mana; CD; range / radius / duration) |
|---|---|
| Stone Aegis | Rampart Strike 55/35/7/5/2.1; Windward Guard 120/40/12/0/0/4; Grounding Ring .42/45/11/5/2.8/2.5; Citadel Crash 130/90/70/6/3.4/1.8 |
| Rift Duelist | Rift Lunge 62/35/6/5/1.4; Duelist Wind 1.4/35/12/0/0/4; Counterveil 100/45/14/0/0/3; Redline 155/90/65/5/2.4 |
| Skyline Marksman | Piercing Gale 70/40/7/10; Tailwind 1.1/35/13/0/0/4; Updraft Step 1.8/45/15/0/0/1.5; Horizon Breaker 175/95/70/14 |
| Storm Warden | Arc Bolt 68/35/6/10; Storm Mark 72/45/10/8/2.7; Gale Step 1.5/30/14/0/0/3; Tempest Fall 145/90/70/10/3.5/1.4 |
| Cairn Warden | Kindling Orb 42/30/6/8; Cairn Barrier 145/45/11/0/0/5; Restoring Draft .9/35/13/0/0/5; Sanctuary Field .35/85/70/8/4/4 |
| Tempest Arbiter | Pressure Drop .48/35/7/8/2.5/2.5; Static Lattice 50/50/13/8/2.5/1; Crosswind 1.2/35/14/0/0/4; Eye of Tempest 165/95/72/10/4.2 |
| Ember Bastion | Cinder Ram 58/40/8/5/2.2; Kiln Plate 125/45/13/0/0/4; Ash Ring .32/50/12/6/3/3; Furnace Gate 145/95/76/7/3.8/1.5 |
| Ironroot Colossus | Root Hook 54/40/9/5.5/2; Barkward 140/42/12/0/0/5; Moss Quake .38/50/11/6/3.1/3; Worldroot 125/90/78/7.5/4.1/1.7 |
| Frostveil Sentinel | Ice Lance 60/42/8/6/2; Rime Guard 130/45/13/0/0/4; Hoarfrost Field .42/48/11/7/3.2/3; Whiteout 135/95/74/8/4/1.6 |
| Ashen Vow | Oath Cleave 66/36/7/4.5/1.9; Cinder Oath 105/42/13/0/0/4; Penitent Stride 1.45/35/12/0/0/3; Last Vow 165/90/70/5/2.6 |
| Zephyr Reaver | Gust Cut 68/38/7/5.5/1.5; Windskin 85/38/14/0/0/2.5; Slipstream 1.85/35/11/0/0/2.5; Sky Sunder 180/90/68/6/2.2 |
| Thornbound | Briar Snap 64/36/7/4.7/1.8; Ironbark 115/40/13/0/0/4; Creeping Vines .36/45/11/5.5/2.8/3; Wild Bloom 170/88/70/5/3 |
| Cinderlash | Flare Lash 70/38/6/5.5/1.7; Coal Guard 90/38/14/0/0/3; Sparkskip 1.75/34/10/0/0/2.5; Pyreline 185/92/69/6.5/2.4 |
| Sunspoke Ranger | Sunpin 72/40/7/10/1.6; Glare Ward 80/42/15/0/0/3; Golden Draft 1.15/35/14/0/0/3; Dawnspear 190/95/73/12/2.7 |
| Glaciershard | Shatterbolt 78/42/7/9/2.3; Crystal Sheen 75/45/15/0/0/3; Permafrost .4/50/10/8/3.2/3.5; Avalanche Prism 195/100/75/10/4 |
| Verdant Cantor | Seedburst 52/38/7/7.5/2.1; Chorus Bark 130/42/11/0/0/5; Trailing Song 1.05/34/12/0/0/4; Grove Refrain .5/88/72/8/4.1/5 |
| Umbral Sable | Night Pierce 74/40/7/5.8/1.5; Eclipsed Skin 88/40/14/0/0/2.5; Duskstep 1.9/36/11/0/0/2.3; Black Sun 195/95/70/6/2.5 |
| Lumenweaver | Lumen Orb 48/35/7/7.5/2; Prism Shroud 155/48/11/0/0/5; Guiding Ray .95/32/13/0/0/4; Daybreak Circle .48/90/74/8/4.3/5 |
| Prism Oracle | Refraction 70/42/7/8.5/2.2; Mirror Shell 100/45/14/0/0/4; Angle Shift 1.25/34/13/0/0/3.5; Zenith Array 140/98/76/9/4.2/1.4 |
| Tidebinder | Surge Orb 58/38/7/8/2.2; Foamward 135/43/12/0/0/4.5; Currentwalk 1.2/34/12/0/0/4; Maelstrom Basin 150/94/74/8.5/4.3/1.5 |

Effects are intentionally limited to the current playable authority vocabulary:
area damage, projectile damage, self shield, movement-speed burst, area slow and
area stun. This keeps all new kits functional today. Targeting and each ability's
plain-language behaviour are authored beside the values in `HeroCatalog.cs` and
are presented in the existing HUD tooltip.

## Balance notes

The four vanguards trade damage or mobility for health, shields and reliable
control. Physical carries (Skyline, Sunspoke) are ranged but have weak escapes;
melee carries compensate with sticking power and risk. Assassins receive their
highest movement values but have the lowest health pools. Supports carry the
largest shield/slow durations and deliberately low base damage. Mages have mana
pressure and low health; their ultimate damage peaks only in grouped fights.
Future tuning should first adjust `HeroStats` and ability base values, not add
hero-specific runtime branches.
