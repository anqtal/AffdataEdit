#ifndef AFFDATAEDIT_SLIDE_VISIBILITY_INCLUDED
#define AFFDATAEDIT_SLIDE_VISIBILITY_INCLUDED
// Match AFF note distance: future notes travel from world Z=-100 toward Z=0.
float SlideVisibility(float worldZ)
{
    clip(100.0 - abs(worldZ));
    return saturate((worldZ + 100.0) / 10.0);
}
#endif
