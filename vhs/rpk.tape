# rpk.tape

Output ./rpk-demo.gif
Set Theme "Catppuccin Mocha"
Set FontSize 20
Set TypingSpeed 60ms
Set Padding 20
Set Width 1400
Set Height 1200

# Intro
Type "rpk --help"
Enter
Sleep 1.5
Type "clear"
Enter
Sleep 0.5

# High-level overview

Type "rpk summary"
Enter
Sleep 3

Type "clear"
Enter
Sleep 0.5

Type "rpk servers summary"
Enter
Sleep 3

Type "clear"
Enter
Sleep 0.5

Type "rpk systems summary"
Enter
Sleep 3

Type "clear"
Enter
Sleep 0.5

Type "rpk services summary"
Enter
Sleep 3

Type "clear"
Enter
Sleep 0.5

Type "rpk services subnets"
Enter
Sleep 2

Type "clear"
Enter
Sleep 0.5

# New in v2.0.0 — tags
Type "rpk servers tag add proxmox-node01 prod"
Enter
Sleep 1
Type "rpk servers tag add proxmox-node02 prod"
Enter
Sleep 1
Type "rpk tags show prod"
Enter
Sleep 3

Type "clear"
Enter
Sleep 0.5

# New in v2.0.0 — graph diagrams (preview the Mermaid source)
Type "rpk graph topology | head -15"
Enter
Sleep 3
