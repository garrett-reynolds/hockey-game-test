# What makes an arena look like the NHL on TV

Research for GitHub issue #72. Sizes are in metres, with the original feet and inches in brackets. "(secondary)" means the source does not own the fact. "(estimate)" means my own figure.

## Gist

- The ice is white. The boards are white with a light yellow kick plate. The lines are red and blue. The rule book allows nothing else on the ice without League permission, and logos must not cover markings (Rule 1.1, 1.3).
- Boards are 1.07 m (42 in) high. Glass above them is 2.44 m (8 ft) at the ends and 1.83 m (6 ft) on the sides. Netting hangs behind the ends.
- The big ads are on the dasher boards. Since 2022-23 the NHL swaps them on TV with virtual ads (digitally enhanced dasherboards), so the TV board ads are not the same as the in-arena ads.
- Overhead you see a centre-hung video board (about 8 m tall), thin ribbon boards (about 0.6-0.8 m tall), banners, and a dark bowl. The ice is bright and the stands are dim.
- From the skate-in, the player sees mostly end boards, glass and the first rows of stands. A centre-hung board is behind them. The rule from the map stands: nothing may make the goalie or the gaps around him harder to read.

## 1. Rink base size (Rule 1.2, 1.5, 1.9)

Facts for the spec

| Item | Size | Source |
|---|---|---|
| Rink length x width | 60.96 x 25.91 m (200 x 85 ft) | [1] Rule 1.2 |
| Corner radius | 8.53 m (28 ft) | [1] Rule 1.2 |
| Goal line from end boards | 3.35 m (11 ft) | [1] Rule 1.5 |
| Goal line and crease lines | 5 cm (2 in) wide, red | [1] Rule 1.5, 1.7 |
| Blue lines from goal line | 19.51 m (64 ft), 30 cm (12 in) wide, PMS 286 | [1] Rule 1.5 |
| Centre line | 30 cm (12 in) wide, red, PMS 186, with a distinct repeat pattern | [1] Rule 1.5 |
| Goal line to centre line | 27.13 m (89 ft) (derived: 30.48 m minus 3.35 m) | derived from [1] |
| Centre spot / circle | 30 cm (12 in) spot, 4.57 m (15 ft) radius circle, blue | [1] Rule 1.9 |
| End-zone faceoff spot | 6.10 m (20 ft) from goal line, 6.71 m (22 ft) either side of the centre line | [1] Rule 1.9 |
| End-zone circle | 4.57 m (15 ft) radius, red, 5 cm (2 in) line, spot 0.61 m (2 ft) wide | [1] Rule 1.9 |
| Neutral-zone spots | 1.52 m (5 ft) from each blue line | [1] Rule 1.9 |
| Goal frame | 1.83 m (6 ft) wide inside, 1.22 m (4 ft) high, red posts and bar, white net | [1] Rule 2.1, 2.2 |
| Crease | 1.83 m (6 ft) radius semicircle; side lines 1.37 m (4 ft 6 in) long, 0.30 m (1 ft) outside each post; filled light blue PMS 298 | [1] Rule 1.7 |
| Referee crease | 3.05 m (10 ft) radius red semicircle at the penalty timekeeper | [1] Rule 1.7 |

## 2. Dasher boards and ad panels (Rule 1.3)

The rule gives the board height and colours. It says nothing about ad panel sizes. Panel sizes come from manufacturers and installs.

Facts for the spec

| Item | Size | Source |
|---|---|---|
| Board height above ice | 1.02-1.22 m (40-48 in); ideal 1.07 m (42 in) | [1] Rule 1.3 |
| Board and ice colour | White. Kick plate at the base is light yellow | [1] Rule 1.3 |
| Kick plate height | Not in the rules. About 0.15-0.25 m (6-10 in) (estimate) | estimate |
| Ad band on the boards | About 0.6-0.8 m of the board face between kick plate and the glass channel (estimate) | estimate |
| Example LED dasher panel, Detroit 2010 | 0.76 x 2.84 m (2 ft 6 in x 9 ft 4 in) | [9] (secondary trade news) |
| Example LED dasher near benches, Nashville | about 0.61 x 3.05 m (2 x 10 ft), Daktronics 10 mm | [10] (secondary) |
| Example AHL LED panel, Optec | 0.72 x 2.25 m (2 ft 4 3/8 in x 7 ft 4 1/4 in) | [11] (AHL, not NHL) |
| Dasher makers | Athletica Sport Systems (with Crystaplex acrylic line) says it is a preferred NHL rink supplier; ANC and Daktronics have made LED dashers | [8], [9], [10] |

Digitally enhanced dasherboards (DEDB)

- Launched for 2022-23 with Supponor. AI keying replaces the board ads in the TV picture [4].
- Five ad zones: behind each net and the three ice zones. One advertiser can take a zone or the whole rink [4].
- Ads run in 30-second blocks tied to the game clock. Teams get 120 blocks a game [4].
- The main centre-ice camera shows the virtual ads. In-arena boards still show on close-ups and reverse angles [4].
- Fans complained about distraction, and the NHL looked at changes [5] (secondary).
- Not verified: board panel sizes in the DEDB design, and the exact start date of 2023-24 changes. I found no NHL page that lists them.

Spec hint: paint boards as a row of flat ad panels above a yellow kick plate. Keep the ads behind the net quiet.

## 3. Ice ads and where logos may go (Rule 1.1)

- Rule 1.1: no ice markings except those in the rules, unless the League gives written permission. On-ice logos must not interfere with official markings [1].
- Real broadcasts show a large centre-ice logo (inside the centre circle, and often one in each end zone) and small logos in the neutral zone and behind the nets. The rule text does not name positions or sizes. These are (estimate) from broadcast practice, not from a primary source.
- The paint layer sits under the clear ice. White paint under the ice is why it reads white on TV.

Facts for the spec

| Item | Size | Source |
|---|---|---|
| Ice logos allowed | Yes, with League permission, not over markings | [1] Rule 1.1 |
| Centre-ice logo | Inside the 4.57 m (15 ft) radius circle, so under about 9 m across (estimate) | estimate |
| Ice-ad placement | Centre circle, end zone beside the circles, neutral zone between spots (estimate) | estimate |

## 4. The trapezoid (Rule 1.8)

Facts for the spec

| Item | Size | Source |
|---|---|---|
| Width at the goal line | 6.71 m (22 ft): the 1.83 m (6 ft) goal plus 2.44 m (8 ft) from each post | [1] Rule 1.8 (derived) |
| Width at the end boards | 8.53 m (28 ft): 3.35 m (11 ft) from each post to the boards | [1] Rule 1.8 (derived) |
| Side lines | Red, 5 cm (2 in) wide, PMS 186, run from goal line to the end boards and up the kick plate | [1] Rule 1.8 |
| Depth | 3.35 m (11 ft), goal line to boards | [1] Rule 1.5 |

The rule says the lines are 2.13 m (7 ft) outside each crease and 3.05 m (10 ft) from the crease at the boards. The 28 ft at the boards also shows in a second rule [1]. The widths above are my sums from those figures.

## 5. Glass, stanchions and netting (Rule 1.3, 1.4)

Facts for the spec

| Item | Size | Source |
|---|---|---|
| Glass above boards, ends | 2.44 m (8 ft) | [1] Rule 1.3 |
| Glass above boards, sides | At least 1.83 m (6 ft). Older nhl.com text says 1.52 m (5 ft) | [1] Rule 1.3, [2] (older page) |
| Total height of ends (boards + glass) | About 3.51 m (11.5 ft) with 42 in boards (derived) | derived |
| Glass in front of penalty benches | Required | [1] Rule 1.3 |
| Spectator netting | Hung at the ends. Height and type are set by the League. No number in the rules | [1] Rule 1.4 |
| Glass thickness | Tempered 12.7 mm (1/2 in) sides, 15.9 mm (5/8 in) ends in one patent. Not a current spec | [12] (secondary, older) |
| Seamless glass | Sits in a 10 cm (4 in) channel in each dasher panel, held by clips, no stanchions | [12] (secondary) |
| Stanchions | Used for acrylic systems. Thin vertical posts every panel width | [12] (secondary) |

Spec hint: show glass as nearly clear, with a faint reflection and thin frame lines at panel joins. Netting is thin black mesh over the glass at the ends. It is not a visual wall.

## 6. Centre-hung video boards

Real examples. Heights above the ice were not given in any source I found. A typical underside is about 8-10 m (estimate).

Facts for the spec

| Arena | Size | Source |
|---|---|---|
| UBS Arena (Islanders) | Four main faces, each 7.9 x 10.5 m (26 x 34.5 ft). Four corner strips 7.9 m x 1 m (26 x 3 ft). Four underbelly faces 2.4 x 7.0 m (8 x 23 ft). Whole package 12 displays, 440 m2 (4,730 sq ft), 5.9 mm pixel pitch, rig rated 40,800 kg (90,000 lb) | [13] Daktronics, [14] |
| Little Caesars Arena (Red Wings) | Four faces, each 8.5 x 13.3 m (28 x 43.5 ft). Underbelly 2.1 x 2.9 m (7 x 9.5 ft) each. Ribbons 0.76 m (2.5 ft) high, 325 m and 279 m long (1,066 and 915 ft) | [6] NHL.com |
| Climate Pledge Arena (Kraken) | No centre-hung. Two three-sided boards over the offensive zones. End faces 7.6 x 14.0 m (25 x 46 ft). Side faces 7.0 x 13.1 m (23 x 43 ft). About 253 m2 (2,720 sq ft) each | [15], [16] |
| Rogers Place (Oilers) | 14.0 x 11.0 x 14.0 m (46 x 36 x 46 ft) wide x high x deep, about 40,800 kg (90,000 lb) | [17] (secondary, owner not confirmed) |
| PNC Arena (Hurricanes) | One curved band, 7.6 m high x 45.1 m wide (25 x 148 ft) | [18] Daktronics (secondary news repost) |

Not verified: Bell Centre board size. Height above the ice for every example.

## 7. Banners, ribbon boards, colours

Facts for the spec

| Item | Size | Source |
|---|---|---|
| Ribbon board height | 0.6-0.8 m (2-2.5 ft); LCA 0.76 m (2.5 ft); Saddledome 0.8 m (2.5 ft) | [6], [19], [20] |
| Ribbon length | LCA 325 m and 279 m (1,066 and 915 ft). Crypto.com Arena three levels of 273-333 m (894-1,093 ft). Saddledome 230 m (753 ft) | [6], [19], [20] |
| Ribbon board position | Fascia of each tier, wraps the bowl (full ring) | [6], [19] |
| Championship and retired-number banners | No NHL standard found. Real banners are about 1.2-1.5 m wide x 2-3 m long (estimate). They hang from the rafters above the end or side stands, and teams place them by choice | [21] (secondary), estimate |
| Banner colours | Team colours. Retired numbers often match the jersey. Cup banners often stand apart | [21] (secondary) |
| Ribbon colours | Full colour LED. Mostly white and team colour text on dark | observation (estimate) |

## 8. Lighting and ice (the TV look)

The NHL's own illuminance rule was not found. The numbers below are install claims from vendors and press. Convert: 1 foot-candle is 10.76 lux.

Facts for the spec

| Item | Value | Source |
|---|---|---|
| Syracuse War Memorial, Ephesus LED | Average 217 fc (about 2,340 lux), up from 105 fc | [22] (secondary, vendor claim) |
| Bridgestone Arena, Ephesus | Average 214 fc (about 2,300 lux) | [22] (secondary) |
| Bell Centre | 1,800 lux, up from 900 lux, after broadcaster complaints about HD | [23] (secondary, 2012) |
| Crypto.com Arena, Musco, 2022 | Vertical illuminance doubled, horizontal nearly doubled, to meet NHL guidelines. No absolute number given | [24] AEG |
| Herb Brooks Arena (college) | 150 fc (about 1,615 lux), above NCAA broadcast standard | [22] (secondary) |
| Maverik Center | LED fitted to meet NHL TV standards. No number | [22] (secondary) |
| Typical broadcast target | About 1,600-2,300 lux on the ice (estimate from the rows above). The "125-150+ fc" figure was not confirmed by any NHL source | estimate |
| Ice colour | White paint under the ice. Rule 1.3 says surface is white | [1] |
| Bowl | Stands dim, ice bright. Light shows use coloured moving lights (for example Elation PROTEUS at Crypto.com Arena) | [24], [25] (secondary) |

Spec hint: the ice should be the brightest large area. Keep the stands darker and cooler. Use a slight bloom, and a soft bright reflection of the boards on the ice.

## What the player sees from the skate-in

Set-up. The player faces the net and skates from 22 m to 9 m from the goal line. Eye height 1.5 m. On the centre line. 16:9 screen, 70 degrees horizontal (try 60-75). Vertical FOV is then about 43 degrees (21.5 degrees up and down) from the 16:9 shape. The end boards are 3.35 m behind the goal line. All numbers below are my geometry (estimate).

| Quantity | At 22 m out | At 9 m out |
|---|---|---|
| Distance to end boards | 25.35 m | 12.35 m |
| Top of 42 in boards (1.07 m) | 1.0 deg below the horizon | 2.0 deg below |
| Top of glass (3.51 m) | 4.5 deg above | 9.2 deg above |
| Top of view at the end wall | 11.5 m above ice | 6.4 m above ice |
| Width seen at the end wall (70 deg) | 35.6 m, wider than the 25.9 m rink, so side boards show | 17.2 m, about two thirds of the rink |
| Net (1.83 m) angle | 4.8 deg, 7 percent of screen width | 11.6 deg, 17 percent |
| Trapezoid at the boards (8.53 m) | 19 deg, 27 percent of width | 38 deg, 54 percent |
| End-zone faceoff spots (6.71 m off centre, 6.10 m in front of goal line) | 15.9 m ahead, 22 deg off axis | 2.9 m ahead of the player, 66 deg off axis: outside the view |

What is in view:

- Foreground and middle: the net, crease, trapezoid lines running from the posts to the boards, and the end-zone faceoff circles at the sides. The circles are at the screen edge at 22 m out and leave the view as the player closes in.
- Behind the net: the end boards, the yellow kick plate, the ad band and the glass. The glass top sits in the lower third of the screen at 22 m and rises to just below the middle of the view at 9 m.
- Above the glass: the first rows of the end-zone stands, then upper tiers and the fascia ribbon (if the ribbon is at that height). At 22 m the view reaches about 11.5 m above ice, so it reaches the lower bowl and maybe the ribbon of a low tier. At 9 m it reaches about 6.4 m, mostly the lower bowl.
- Sides: the corners are partly in view at 22 m and are outside the view at 9 m.
- Ice ads: the area between the faceoff circles and behind the net is seen at a shallow angle. Ice ads there are foreshortened.

The centre-hung board.

- It hangs at the rink centre, 27.1 m from the goal line. The player faces away from it. It is behind the camera and never seen in the skate-in.
- Real arenas use alternatives that can be seen from the end zone. Climate Pledge Arena hangs two three-sided boards over the offensive zones (7.6 x 14.0 m end faces) [15]. Options: a board over each end zone, a wide LED screen on the upper bowl facade behind each net, or a ribbon board along the end fascia.
- If a board hangs over the slot, it would sit above the top of the view at 9 m (it would be above 3.5 m at 5 m ahead of the player), so it would be visible only at long range, and only as an edge.

What to keep calm (the map's firm rule).

- Art must not make the goalie or the gaps around him harder to read. Keep the area behind the net calm: no animated ads, no bright flashing, no strong colour near the net frame or between the goalie's pads and the posts.
- The white net and red posts sit against white boards at the back of the net. Keep the board face behind the net low in contrast and a flat neutral tone. Put brightest ad colours at the sides, not directly behind the net.
- Keep the glass reflections low. Keep the ice logo out of the crease and the lines near the posts.
- Put animated LED content high (ribbon, end-wall screen) and dim it while the Shot is live.
- Choose ad colours away from the puck colour (black) and away from the goalie's gear colours.

## Facts I could not verify

- The NHL's own illuminance targets (lux or foot-candles, vertical or horizontal).
- Ad panel sizes for the dashers in the NHL. Only vendor installs.
- The kick plate height. The rules give only the colour.
- Heights above ice of centre-hung boards. Bell Centre board size.
- Banner sizes. Ice-logo sizes and positions.
- The current Rule 1.3 gives 1.83 m (6 ft) side glass. An older nhl.com page says 1.52 m (5 ft).

## Sources

1. NHL Official Rules 2026-2027, Rule 1 and Rule 2 (read from the PDF). https://media.nhl.com/site/asset/public/ext/2026-27/2026-27Rules.pdf
2. NHL rules page (older text, glass 5 ft along sides). https://www.nhl.com/ice/page.htm?id=24935 (page now returns 404; text seen via search summary)
3. NHL Official Rules 2024-25 PDF (listed, not read). https://media.nhl.com/site/asset/public/ext/2024-25/2024-25Rules.pdf
4. ESPN, NHL to debut digitally enhanced dasherboards. https://www.espn.com/nhl/story/_/id/34717938
5. ESPN, NHL considers changes to digital ad boards. https://www.espn.com/nhl/story/_/id/38394635/nhl-considers-changes-digital-ad-boards-fan-criticisms
6. NHL.com, Little Caesars Arena centerhung. https://www.nhl.com/redwings/news/little-caesars-arena-fans-to-enjoy-world-s-largest-seamless-centerhung-290395034
7. Wikipedia, Digitally enhanced dasherboard (background only). https://en.wikipedia.org/wiki/Digitally_enhanced_dasherboard
8. Athletic Business, Rethinking the Rink (Athletica, Crystaplex). https://www.athleticbusiness.com/operations/safety-security/article/15140499/rethinking-the-rink
9. LEDinside, ANC Sports at Joe Louis Arena. https://www.ledinside.com/node/11935
10. Daktronics via Nashville Predators dasher news (found by search, page not opened). https://www.sportsvideo.org/?p=32130
11. Optec, Ontario Reign case study. https://optec.com/pdf-case-study-ontario-reign/
12. Google Patents, US7914385B2 Dasher boards. https://patents.google.com/patent/US7914385 and Athletic Business article [8]
13. Daktronics, UBS Arena. https://www.daktronics.com/news/ubs-arena-selects-daktronics-to-install-45-hdr-capable-led-displays
14. Arena Digest, UBS Arena displays. https://arenadigest.com/2021/05/06/daktronics-to-power-ubs-arena-display-tech/
15. Athletic Business, Daktronics at Climate Pledge Arena. https://www.athleticbusiness.com/industry-press-room/article/15280571/daktronics-led-display-system-lights-up-climate-pledge-arena-fan-experience
16. NHL.com, Climate Pledge Arena tour. https://www.nhl.com/wild/news/climate-pledge-arena-tour-102721-327303056
17. Rogers Place board size, found by search summary (owner and URL not confirmed). Treat as unverified.
18. Sports Video Group, PNC Arena Daktronics centerhung. https://www.sportsvideo.org/2019/04/18/pnc-arena-upgrades-carolina-hurricanes-games-with-new-daktronics-centerhung/
19. Daktronics, Crypto.com Arena lighting/ribbon news. https://www.businesswire.com/news/home/20221007005207/en
20. Sign Media Canada, Calgary Flames scoreboard. https://www.signmedia.ca/?p=24966
21. Hockey News, Panthers banners. https://thehockeynews.com/nhl/florida-panthers/latest-news/florida-panthers-rearrange-championship-banners-which-are-now-in-chronological-order
22. Athletic Business, LED lighting in sports arenas (Ephesus examples). https://www.athleticbusiness.com/facilities/stadium-arena/article/15144322/led-lighting-finding-a-home-in-major-sports-arenas and Ephesus Herb Brooks case study https://www.assets.cooperlighting.com/is/content/CLS/Assets/cooper-lighting/case-studies/ephesus-lumadapt-8-herb-brooks-casestudy.pdf
23. Sports Business Daily, Bell Centre lighting. https://www.sportsbusinessdaily.com/Journal/Issues/2012/10/22/Franchises/Bell-Centre.aspx
24. AEG Worldwide, Crypto.com Arena case study (Musco). https://aegworldwide.com/node/971731
25. Live Design, Elation PROTEUS at Crypto.com Arena. https://www.livedesignonline.com/news/140-proteus-transform-fan-experience-cryptocom-arena
