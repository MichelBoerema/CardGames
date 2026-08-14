# Mexico Game Scene Setup Guide

This guide walks you through setting up the MexicoGame scene with UI, dice input, and game flow.

## Phase 1: Create Canvas Hierarchy

### 1.1 Root Canvas
1. Create new Canvas (Right-click in hierarchy → UI → Canvas)
   - Set to "Screen Space - Overlay"
   - Set Canvas Scaler: Reference Resolution 1920 x 1080

### 1.2 Game UI Panel (Main Container)
Create a Panel under Canvas:
```
Canvas
├── GameUIPanel (Panel, anchored full screen, color transparent)
│   ├── CurrentPlayerIndicator (Panel, top-center)
│   │   ├── PlayerNameText (Text, "YOUR TURN")
│   │   └── PlayerAvatarImage (Image, circular or square)
│   │
│   ├── LeaderIndicatorText (Text, top-left, "Dealer: PlayerName")
│   │
│   ├── RoundResultsPanel (Panel with ScrollRect, center-left)
│   │   └── Scroll View
│   │       └── Content (VerticalLayoutGroup)
│   │           └── (RollEntry prefab instances spawn here)
│   │
│   ├── DiceInputArea (Panel with CanvasGroup, center-bottom)
│   │   └── DiceVisual (Image with RectTransform, shows dice icon)
│   │
│   ├── LoserDisplayPanel (hidden by default, center)
│   │   ├── LoserAvatarImage (Image)
│   │   ├── LoserNameText (Text)
│   │   └── LoserStatusText (Text, "You Lost!")
│   │
│   └── LeaveButton (Button, bottom-right)
└── PopupsRoot (Panel for generic popups, transparent)
    └── (InfoPopup spawns here via UIManager)
```

### 1.3 Current Player Indicator Setup
Path: `Canvas/GameUIPanel/CurrentPlayerIndicator`

1. Create Panel component
   - LayoutElement: Preferred Width 300, Preferred Height 80
   - Background Image enabled, slight transparency
   - Anchored top-center, offset from top 20px

2. Add children:
   - **PlayerAvatarImage** (Image component)
     - Size: 60x60
     - Placed left side of panel
   
   - **PlayerNameText** (Text component)
     - Font size 32
     - Text alignment: center, middle
     - Content: "YOUR TURN" or "{PlayerName}'s Turn"

### 1.4 Leader Indicator Text
Path: `Canvas/GameUIPanel/LeaderIndicatorText`

1. Create Text component
   - Content: "Dealer: {PlayerName}"
   - Font size 24
   - Anchored top-left with 20px offset
   - Color: Gold or highlight color

### 1.5 Round Results Panel (Persistent Roll History)
Path: `Canvas/GameUIPanel/RoundResultsPanel`

1. Create Panel with ScrollRect:
   - Size: 400x500 (adjust based on your needs)
   - Anchored left side, vertically centered
   - Background Image with slight transparency
   - ScrollRect component:
     - Content: Child panel with VerticalLayoutGroup
     - Scrollbar: Create vertical scrollbar on right edge (optional)

2. Create Content Panel under RoundResultsPanel:
   - VerticalLayoutGroup component:
     - Child Force Expand Height: OFF
     - Preferred Height: ON
   - Layout Element:
     - Preferred Width: 380
     - Preferred Height: enabled (auto-expands)

### 1.6 Dice Input Area (Bottom Center)
Path: `Canvas/GameUIPanel/DiceInputArea`

1. Create Panel:
   - Size: 300x350
   - Anchored bottom-center, 50px from bottom
   - CanvasGroup component (very important!):
     - **Save reference for fade animations**
   - Background Image (optional visual indicator)
   - **Attach DiceThrowInput script to this Panel**

2. Create OneDiceVisual child (Image):
   - Size: 150x150
   - Centered in panel
   - Image: Single die sprite
   - **Visible only during starting roll-off phase**

3. Create TwoDiceVisual child (Container with 2 dice):
   - Option A: Single Image showing 2 dice side-by-side (size 300x150)
   - Option B: Two child Images (each 150x150, positioned side-by-side)
   - **Visible only during game play phase**

4. Configure DiceThrowInput (on DiceInputArea):
   - minUpwardDistance: 80
   - minUpwardSpeed: 300
   - dragClamp: 250
   - Dice Visual: (DiceInputArea's own RectTransform, so it encompasses both children)

5. (Optional) Add hint text as child:
   - Content: "Swipe up to throw"
   - Font size 16
   - Positioned below dice area

### 1.7 Loser Display Panel (Hidden by Default)
Path: `Canvas/GameUIPanel/LoserDisplayPanel`

1. Create Panel:
   - Size: 400x300
   - Anchored center
   - **CanvasGroup component: set alpha = 0 initially**
   - Background Image with semi-transparent dark overlay

2. Add children:
   - **LoserAvatarImage** (Image)
     - Size: 100x100
     - Top-center of panel
   
   - **LoserNameText** (Text)
     - Content: "{PlayerName}"
     - Font size 28
     - Below avatar
   
   - **LoserStatusText** (Text)
     - Content: "You Lost!" or "{PlayerName} Lost!"
     - Font size 24
     - Below name text
     - Color: Red or warning color

## Phase 2: Create Roll Entry Prefab

This prefab is instantiated for each player roll and added to RoundResultsPanel's Content.

### 2.1 Create RollEntry Prefab Structure
1. Create new Panel GameObject: `Prefabs/Mexico/RollEntry`
   - LayoutElement: Preferred Height 60
   - LayoutElement: Preferred Width (match parent width)
   - HorizontalLayoutGroup:
     - Child Force Expand Width: OFF
     - Padding: 5px on all sides

2. Add children (left to right):
   ```
   RollEntry (Panel, horizontal layout)
   ├── AvatarImage (Image, size 50x50)
   ├── PlayerName (Text, "Player Name", size 18)
   ├── DiceDisplay (Text, "1-1", size 18)
   ├── Rank (Text, "Rank", size 16)
   └── RankLabel (Text, "Regular", size 14, color: gray)
   ```

3. Sample layout setup:
   - AvatarImage: 50x50, LayoutElement Preferred Width 50
   - PlayerName: LayoutElement Preferred Width 100
   - DiceDisplay: LayoutElement Preferred Width 60
   - Rank: LayoutElement Preferred Width 40
   - RankLabel: LayoutElement Preferred Width 80 (flex)

4. Save as prefab: `Assets/Prefabs/Mexico/RollEntry.prefab`

## Phase 3: Wire MexicoUIManager in Scene

### 3.1 Create MexicoUIManager GameObject
1. Create empty GameObject in scene hierarchy
   - Name: "MexicoUIManager"
   - Position: (0, 0, 0) - doesn't matter, no visuals

2. Attach MexicoUIManager script

### 3.2 Assign References in Inspector
With MexicoUIManager selected, drag-drop these UI elements into the inspector fields:

```
MexicoUIManager Component:
├── Current Player UI
│   ├── currentPlayerText = Canvas/GameUIPanel/CurrentPlayerIndicator/PlayerNameText
│   ├── currentPlayerAvatarImage = Canvas/GameUIPanel/CurrentPlayerIndicator/PlayerAvatarImage
│   └── leaderIndicatorText = Canvas/GameUIPanel/LeaderIndicatorText
├── Round Results Panel
│   ├── roundResultsPanel = Canvas/GameUIPanel/RoundResultsPanel
│   ├── roundResultsContent = Canvas/GameUIPanel/RoundResultsPanel/[ScrollView Content]/Content
│   ├── rollEntryPrefab = Prefabs/Mexico/RollEntry.prefab
│   └── maxPanelHeight = 400
├── Loser Display
│   ├── loserDisplayPanel = Canvas/GameUIPanel/LoserDisplayPanel
│   ├── loserAvatarImage = Canvas/GameUIPanel/LoserDisplayPanel/LoserAvatarImage
│   ├── loserNameText = Canvas/GameUIPanel/LoserDisplayPanel/LoserNameText
│   └── loserStatusText = Canvas/GameUIPanel/LoserDisplayPanel/LoserStatusText
├── Dice Input
│   ├── diceThrowInput = Canvas/GameUIPanel/DiceInputArea (the Panel with DiceThrowInput script)
│   ├── diceInputCanvasGroup = Canvas/GameUIPanel/DiceInputArea (the CanvasGroup)
│   ├── oneDiceVisual = Canvas/GameUIPanel/DiceInputArea/OneDiceVisual
│   └── twoDiceVisual = Canvas/GameUIPanel/DiceInputArea/TwoDiceVisual
└── Popups
    ├── rollNotificationDuration = 2.0
    └── loserAnnouncementDuration = 3.0
```

### 3.3 Wire Dice Throw Event
In MexicoUIManager.Start() or later, call:
```csharp
public void OnEnable()
{
    WireDiceThrowEvent(); // Connects DiceThrowInput.OnThrow to RequestRollServerRpc
}
```

This is already implemented, but ensure Start() completes before dice can be thrown.

## Phase 4: Scene Architecture Summary

```
MexicoGame Scene Hierarchy:
├── NetworkManager (existing prefab)
├── GameManager (existing prefab)
├── MexicoGameManager (new)
├── Player (spawned, one per connected client)
├── MexicoUIManager (new)
└── Canvas
    ├── GameUIPanel
    │   ├── CurrentPlayerIndicator
    │   ├── LeaderIndicatorText
    │   ├── RoundResultsPanel (with ScrollRect)
    │   ├── DiceInputArea (with DiceThrowInput)
    │   ├── LoserDisplayPanel
    │   └── LeaveButton
    └── PopupsRoot (for generic popups)
```

## Phase 5: Testing Checklist

### Play Mode (Single Player Test)
- [ ] Scene loads without errors
- [ ] MexicoUIManager.Instance is not null
- [ ] DiceThrowInput component is registered with OnThrow event
- [ ] Current player indicator displays text "YOUR TURN"
- [ ] Dice visual is clickable/draggable
- [ ] Throw a dice (swipe up) - should trigger OnThrow event without errors
- [ ] Notification popup appears briefly showing mock roll

### Multiplayer Test (2+ Players via Netcode)
- [ ] All players join MexicoGame scene
- [ ] Starting roll-off broadcasts to all clients (see notifications)
- [ ] Highest die player is selected as starting leader
- [ ] Leader indicator shows "Dealer: {LeaderName}"
- [ ] Current player indicator shows correct player on each client
- [ ] Leader throws dice (swipe up):
  - Roll notification appears on all clients
  - Roll entry appears in RoundResultsPanel on all clients
  - Leader can throw again until done (max 3 times)
- [ ] Leader stops rolling (if implemented) or exhausts rolls:
  - Next player in order becomes active
  - Current player indicator updates on all clients
- [ ] If leader rolls Mexico (2-1):
  - "Mexico!" notification appears
  - Lead passes to next player immediately
  - Leader indicator updates
- [ ] When all players have rolled:
  - Loser is determined (lowest rank)
  - Loser display panel fades in from top center
  - Shows loser's avatar, name, and status ("You Lost!" or "PlayerName Lost!")
  - Display fades out after 3 seconds
  - New round starts with loser as leader
  - RoundResultsPanel clears for new round
- [ ] Spectator test (optional):
  - Join as 3rd player after 2 are already playing
  - DiceThrowInput should be disabled (not interactable)
  - Can still see all rolls in RoundResultsPanel

### UI Visual Polish
- [ ] Text is readable (font sizes, contrast)
- [ ] Avatar images display correctly (use AvatarDatabase)
- [ ] Popup animations fade smoothly (not instant)
- [ ] No overlapping UI elements
- [ ] Loser display has good visual contrast
- [ ] Dice visual feedback on drag (scale boost, slight tilt optional)

## Phase 6: Optional Enhancements (Defer to Later)

1. **Dice Throw Animation:**
   - Currently dice visual just scales on throw
   - Future: Replace with 3D animated dice or sprite animation
   - Uses IDiceVisual interface pattern (see MexicoUIManager comments)

2. **Sound Effects:**
   - Hook `MexicoUIManager.OnPlayerRolled()` to play dice roll sound
   - Hook `MexicoUIManager.OnRoundLost()` to play loser buzzer/ding
   - Use `BarAudioManager.Instance.PlaySound()`

3. **Mobile Responsive Design:**
   - Current DiceThrowInput uses pixel-based thresholds
   - On mobile, may need to scale by screen height/width ratio
   - Adjust minUpwardDistance and minUpwardSpeed based on canvas scale

4. **Accessibility:**
   - Add option to show "Swipe up to throw" hint
   - Add keyboard support (SPACE to throw for testing)
   - Add color-blind friendly color scheme

## Troubleshooting

**Issue: MexicoUIManager.Instance is null**
- Ensure MexicoUIManager GameObject exists in scene
- Ensure MexicoUIManager script is attached
- Ensure Awake() runs before Start()

**Issue: Roll doesn't appear in RoundResultsPanel**
- Check rollEntryPrefab is assigned in inspector
- Check roundResultsContent is pointing to VerticalLayoutGroup (not the panel itself)
- Check RollEntry prefab has correct child structure (PlayerName, DiceDisplay, Rank, RankLabel)

**Issue: Current player indicator doesn't update**
- Check currentPlayerText is assigned
- Check MexicoGameManager.NotifyActivePlayer() is being called
- Verify Player.SetTurnClientRpc() is called by MexicoGameManager

**Issue: Dice throw doesn't register**
- Check DiceThrowInput is attached to DiceVisual (Image)
- Check DiceThrowInput.OnThrow event is hooked in MexicoUIManager.WireDiceThrowEvent()
- Verify MexicoGameManager.Instance exists and RequestRollServerRpc() is public

**Issue: Loser display doesn't appear**
- Check loserDisplayPanel is assigned in inspector
- Check loserDisplayPanel has CanvasGroup component
- Verify MexicoGameManager.RoundLost event is subscribed in MexicoUIManager
- Check CanvasGroup alpha is being set to 0 initially

---

**Next Steps:**
1. Create the Canvas hierarchy as described above
2. Create the RollEntry prefab
3. Assign all references in MexicoUIManager inspector
4. Run scene in play mode and test single-player flow
5. Test with multiplayer (2+ players) using Netcode
6. Polish UI visuals and animations
7. Add optional enhancements (sound, 3D dice, etc.)

For questions, refer to the BusGame or BluffGame UI setup as reference - they follow similar patterns with game-specific UIManagers.
