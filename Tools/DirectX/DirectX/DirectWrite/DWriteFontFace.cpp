// Copyright (c) Microsoft Corporation.  All rights reserved.

#include "stdafx.h"
#include "DWriteFontFace.h"
#include "DWriteFontFamily.h"

using namespace Microsoft::WindowsAPICodePack::DirectX::Utilities;

FontFaceType FontFace::FaceType::get()
{
    return static_cast<FontFaceType>(CastInterface<IDWriteFontFace>()->GetType());
}

UINT32 FontFace::Index::get()
{
    return CastInterface<IDWriteFontFace>()->GetIndex();
}

FontSimulations FontFace::Simulations::get()
{
    return static_cast<FontSimulations>(CastInterface<IDWriteFontFace>()->GetSimulations());
}

Boolean FontFace::IsSymbolFont::get()
{
    return CastInterface<IDWriteFontFace>()->IsSymbolFont() != 0;
}

FontMetrics FontFace::Metrics::get()
{
    DWRITE_FONT_METRICS metrics;
    CastInterface<IDWriteFontFace>()->GetMetrics(&metrics);

    return FontMetrics(metrics);
}

UINT16 FontFace::GlyphCount::get()
{
    return CastInterface<IDWriteFontFace>()->GetGlyphCount();
}

cli::array<GlyphMetrics>^ FontFace::GetDesignGlyphMetrics (cli::array<UINT16>^ glyphIndexes, BOOL isSideways)
{
    cli::array<GlyphMetrics>^ glyphMetrics = gcnew cli::array<GlyphMetrics>(glyphIndexes->Length);
    pin_ptr<UINT16> glyphIndexesPtr = &glyphIndexes[0];

    pin_ptr<GlyphMetrics> glyphMetricsPtr = &glyphMetrics[0];
    
    Validate::VerifyResult(CastInterface<IDWriteFontFace>()->GetDesignGlyphMetrics(glyphIndexesPtr, glyphIndexes->Length, (DWRITE_GLYPH_METRICS*)glyphMetricsPtr, isSideways ? 1 : 0));

    return glyphMetrics;
}

cli::array<GlyphMetrics>^ FontFace::GetDesignGlyphMetrics (cli::array<UINT16>^ glyphIndexes)
{
    return GetDesignGlyphMetrics(glyphIndexes, false);
}

cli::array<UINT16>^ FontFace::GetGlyphIndexes (cli::array<UINT32>^ codePoints)
{
    cli::array<UINT16>^ glyphIndexes = gcnew cli::array<UINT16>(codePoints->Length);
    pin_ptr<UINT16> glyphIndexesPtr = &glyphIndexes[0];

    pin_ptr<UINT32> codePointsPtr = &codePoints[0];    
    Validate::VerifyResult(CastInterface<IDWriteFontFace>()->GetGlyphIndicesW(codePointsPtr, codePoints->Length, glyphIndexesPtr));

    return glyphIndexes;
}

cli::array<UINT16>^ FontFace::GetGlyphIndexes (cli::array<System::Char>^ characterArray)
{
    cli::array<UINT16>^ glyphIndexes = gcnew cli::array<UINT16>(characterArray->Length);
    pin_ptr<UINT16> glyphIndexesPtr = &glyphIndexes[0];

    cli::array<UINT32>^ codePoints = gcnew cli::array<UINT32>(characterArray->Length);
    for (int i = 0; i < characterArray->Length; i++)
    {
        codePoints[i] = System::Convert::ToUInt32(characterArray[i]);
    }

    pin_ptr<UINT32> codePointsPtr = &codePoints[0];    
    Validate::VerifyResult(CastInterface<IDWriteFontFace>()->GetGlyphIndicesW(codePointsPtr, codePoints->Length, glyphIndexesPtr));

    return glyphIndexes;
}

cli::array<UINT16>^ FontFace::GetGlyphIndexes(System::String^ text)
{
    cli::array<UINT16>^ glyphIndexes = gcnew cli::array<UINT16>(text->Length);
    pin_ptr<UINT16> glyphIndexesPtr = &glyphIndexes[0];

    cli::array<UINT32>^ codePoints = gcnew cli::array<UINT32>(text->Length);
    for (int i = 0; i < text->Length; i++)
    {
        codePoints[i] = System::Convert::ToUInt32(text[i]);
    }

    pin_ptr<UINT32> codePointsPtr = &codePoints[0];    
    Validate::VerifyResult(CastInterface<IDWriteFontFace>()->GetGlyphIndicesW(codePointsPtr, codePoints->Length, glyphIndexesPtr));

    return glyphIndexes;
}