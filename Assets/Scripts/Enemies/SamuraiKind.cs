// How a samurai fights. The army's rows take their kind from LevelConfig.rowKinds. The save keeps these names (the
// encyclopedia's Foes ids), so never rename a value; a new kind also needs a Foes.All entry. Never add a kind named
// Elite: that id is the encyclopedia's Foes.Elite, the round's Elite that any kind can be (Samurai.MakeElite).
public enum SamuraiKind
{
    Ashigaru,
    // Looses a fan of three arrows.
    Archer,
    // A commander in the back rank. Tougher, and hurls an unbreakable spear over its own men.
    Taisho,
    // A front-rank wall: takes two hits and never fires, while the ranks behind shoot over it.
    Shieldbearer,
    // A ghost of a fallen samurai: translucent, and its paired arrows drift straight through cover.
    Yurei,
    // An Imperial shrine guard with a hand bell: never shoots, but its toll rouses the whole army to fire faster.
    Bellringer,
}
