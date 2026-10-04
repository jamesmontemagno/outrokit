---
name: Podcast Metadata Generator
description: Blank Tape J-Card visual system for the public site.
colors:
  tape-blue: "oklch(41% 0.2 268)"
  tape-blue-deep: "oklch(30% 0.16 268)"
  card-stock: "oklch(98.5% 0.004 100)"
  ink: "oklch(19% 0.025 268)"
  ink-soft: "oklch(40% 0.05 268)"
  leader-yellow: "oklch(88% 0.17 93)"
  record-red: "oklch(65% 0.22 31)"
  record-red-ink: "oklch(50% 0.2 29)"
  ruled-blue: "oklch(41% 0.2 268 / 0.26)"
  tape-brown: "oklch(31% 0.035 55)"
typography:
  display:
    fontFamily: "Archivo, Helvetica Neue, Arial, sans-serif"
    fontSize: "clamp(2.25rem, 1.2rem + 2.3vw, 3.25rem)"
    fontWeight: 800
    lineHeight: 0.98
    letterSpacing: "-0.025em"
  section-heading:
    fontFamily: "Archivo, Helvetica Neue, Arial, sans-serif"
    fontSize: "clamp(2rem, 0.9rem + 4.4vw, 4.25rem)"
    fontWeight: 800
    lineHeight: 0.96
    letterSpacing: "-0.02em"
  title:
    fontFamily: "Archivo, Helvetica Neue, Arial, sans-serif"
    fontSize: "clamp(1.3rem, 1.1rem + 0.7vw, 1.625rem)"
    fontWeight: 700
    lineHeight: 1.15
    letterSpacing: "-0.01em"
  body:
    fontFamily: "Archivo, Helvetica Neue, Arial, sans-serif"
    fontSize: "1.0625rem"
    fontWeight: 400
    lineHeight: 1.55
  printed-label:
    fontFamily: "Archivo, Helvetica Neue, Arial, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 650
    lineHeight: 1.3
    letterSpacing: "0.08em"
  mono:
    fontFamily: "Sometype Mono, ui-monospace, SF Mono, Menlo, Consolas, monospace"
    fontSize: "0.9375rem"
    fontWeight: 500
    lineHeight: 1.5
rounded:
  tick: "1px"
  focus: "2px"
  label: "3px"
  card: "4px"
  screen: "6px"
  cassette: "0.6rem"
  pill: "999px"
spacing:
  gutter: "clamp(1rem, 3vw, 2.5rem)"
  ruled-line: "1.625rem"
  jcard-padding: "clamp(1.25rem, 2.5vw, 2.25rem)"
  side-y: "clamp(3.5rem, 8vw, 6.5rem)"
  side-gap: "clamp(1.25rem, 3vw, 2.5rem)"
  track-y: "clamp(1.75rem, 3.5vw, 2.75rem)"
components:
  button-primary:
    backgroundColor: "{colors.tape-blue}"
    textColor: "{colors.card-stock}"
    typography: "{typography.body}"
    rounded: "{rounded.card}"
    padding: "0.8rem 1.2rem"
  button-primary-hover:
    backgroundColor: "{colors.tape-blue-deep}"
    textColor: "{colors.card-stock}"
    rounded: "{rounded.card}"
  button-ink:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.card-stock}"
    rounded: "{rounded.card}"
    padding: "0.8rem 1.2rem"
  command:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.card-stock}"
    typography: "{typography.mono}"
    rounded: "{rounded.card}"
  copy-button:
    backgroundColor: "{colors.leader-yellow}"
    textColor: "{colors.ink}"
    typography: "{typography.printed-label}"
    padding: "0 1rem"
  coated-card:
    backgroundColor: "{colors.card-stock}"
    textColor: "{colors.ink}"
    rounded: "{rounded.card}"
---

# Design System: Podcast Metadata Generator

## Overview

**Creative North Star: "Blank Tape J-Card"**

The public site is a 1975-95 blank cassette inlay card, unfolded on a tape-blue table. It uses white coated-card panels, ruled index lines, a leader-yellow spine, record-red side blocks, packaging stripes, and a cassette shell to make generated metadata feel like labels being written onto physical media.

The system is graphic and tactile, not app-chrome or generic SaaS. Reading regions stay on white card stock for legibility, while tape blue owns the page ground and outro, leader yellow owns the spine and liner-notes region, and record red marks action, side identity, and typed-cursor emphasis. The one authored motion is the index card typing itself while cassette reels turn; the same content is present in the HTML before motion starts and motion is skipped under `prefers-reduced-motion`.

**Key Characteristics:**
- Unfolded J-card structure: `.jcard`, `.jcard__front`, `.jcard__spine`, `.jcard__index`, and `.jcard__flap` are the core layout vocabulary.
- Tape-package color ownership: blue field, white card stock, yellow reference/spine, red side and stamp marks.
- Archivo carries printed labels and display type; Sometype Mono is reserved for commands, filenames, generated examples, and terminal output.
- Surfaces are mostly flat coated paper, with selective cassette and card shadows to lift the physical object from the blue field.
- Example episode content stays made up and labeled as an example.

## Colors

The palette is a committed blank-cassette package: saturated blue ground, warm white card stock, yellow leader tape, red recording marks, and dark ink.

### Primary
- **Tape Blue** (`tape-blue`): Page ground, primary buttons, links on card surfaces, packaging stripe, Side B block, and brand-level field color.
- **Deep Tape Blue** (`tape-blue-deep`): Primary button hover and darker blue state.

### Secondary
- **Leader Yellow** (`leader-yellow`): J-card spine, liner-notes section, cassette band, copy-button default, bonus track tag, and hover fill for tick boxes.

### Tertiary
- **Record Red** (`record-red`): Side A blocks, cassette side mark, packaging stripe, and failure state for copy buttons.
- **Record Red Ink** (`record-red-ink`): Hero turn word, stamp border/text, track numbers, and typed cursor.

### Neutral
- **Coated Card Stock** (`card-stock`): Main J-card, reading sections, cassette label, command text, checked-copy state, and white stripes.
- **Dark Ink** (`ink`): Body text, terminal/command surfaces, cassette shell, ink buttons, selected OS pill, and focus color on light surfaces.
- **Soft Ink** (`ink-soft`): Secondary labels, field labels, asides, captions, and muted table headers.
- **Ruled Blue** (`ruled-blue`): Index-card rules, table dividers, and light card borders.
- **Tape Brown** (`tape-brown`): Cassette reel tape fill only.

### Named Rules
**The Field and Card Rule.** Blue owns the outside world; card stock owns reading and instruction. Do not turn the main reading sections blue or move the hero to a dark terminal field.

**The Four Package Colors Rule.** New visual elements should come from tape blue, card stock, leader yellow, record red, and ink before introducing any new hue.

## Typography

**Display Font:** Archivo (with Helvetica Neue, Arial, sans-serif fallback)  
**Body Font:** Archivo (with Helvetica Neue, Arial, sans-serif fallback)  
**Label/Mono Font:** Sometype Mono for typed/generated/user-entered material; Archivo condensed for printed labels.

**Character:** Archivo stretches from condensed printed labels to expanded display headlines, matching the packaging-world contrast between small manufactured labels and loud cover type. Sometype Mono reads as the tool's writing hand: commands, filenames, generated examples, subtitles, and the terminal sketch.

### Hierarchy
- **Display** (Archivo, 125% stretch, 800 weight, `clamp(2.25rem, 1.2rem + 2.3vw, 3.25rem)`, 0.98 line-height): Hero `h1`; balanced, expanded, and slightly tight with -0.025em tracking.
- **Section Heading** (Archivo, 125% stretch, 800 weight, `clamp(2rem, 0.9rem + 4.4vw, 4.25rem)`, 0.96 line-height, uppercase): `.side h2`, `.notes h2`, and `.outro__line`.
- **Title** (Archivo, 100% stretch, 700-750 weight, `clamp(1.3rem, 1.1rem + 0.7vw, 1.625rem)`, 1.15 line-height): `.track h3` and `.notes h3`.
- **Body** (Archivo, 100% stretch, 400 weight, `1.0625rem`, 1.55 line-height): Main paragraphs and reading content. Intro paragraphs rise to `1.125rem`.
- **Printed Label** (Archivo, 70% stretch, 650-700 weight, `0.75rem` or `0.875rem`, 0.08em tracking, uppercase): `.printed`, field labels, table headers, track numbers, copy buttons, stamps, and format labels.
- **Typed Mono** (Sometype Mono, 400-700 weight, usually `0.8125rem` to `0.9375rem`): `.mono`, `.cmd pre`, `.screen__glass`, generated demo fields, filenames, SRT, and command strings.

### Named Rules
**The Typed Output Rule.** Use Sometype Mono only for what the visitor types or what the tool writes; do not use it for marketing headings, navigation, or general body copy.

**The Printed Label Rule.** Small functional labels should be uppercase Archivo at condensed widths, usually 70% stretch with 0.08em tracking.

## Layout

The page is built as one public landing page with a cassette program structure: hero J-card, packaging stripes, Side A, Side B, liner notes, and outro. `.wrap` caps most content at `73.75rem` with a responsive gutter of `clamp(1rem, 3vw, 2.5rem)`.

The hero object is `.jcard`, a `max-width: 80rem` grid with columns `minmax(0, 5fr) 3.5rem minmax(0, 7fr)`: front panel, fixed spine, and index panel. The front and index panels share `clamp(1.25rem, 2.5vw, 2.25rem)` padding. The `.jcard__flap` spans all columns and behaves like folded package copy.

Instruction sections use `.side` with vertical padding from `clamp(3.5rem, 8vw, 6.5rem)` to `clamp(2rem, 4vw, 3rem)`. `.side__head` is a three-column header: side letter, text, and OS switch. `.track` rows are two-column grids (`20rem` name column, `40rem` body column) with `2px` dark-ink top borders and vertical rhythm from `clamp(1.75rem, 3.5vw, 2.75rem)`.

Responsive rules are explicit: at `68rem` the J-card becomes one column and the spine rotates horizontal; at `60rem` side headers and tracks collapse; at `40rem` fields, charts, formats, and command copy buttons become single-column. Short wide screens (`min-width: 68.0625rem` and `max-height: 51rem`) reduce hero height, headline size, and cassette size so the install button stays visible.

### Named Rules
**The Tape Program Rule.** Extensions should keep the Side A / Side B program model: side letters identify major flows, `.tracks` hold sequential steps, and liner notes hold reference material.

## Elevation & Depth

Depth is mostly physical-paper layering, not generic card stacks. The J-card casts the largest shadow on the blue field, the cassette has a smaller plastic-shell shadow, the spine and flap use inset fold shadows, and command/screen surfaces rely on dark tonal contrast rather than elevation.

### Shadow Vocabulary
- **J-card lift** (`0 30px 50px -22px oklch(15% 0.1 268 / 0.75), 0 8px 16px -8px oklch(15% 0.1 268 / 0.5)`): Only for the unfolded `.jcard` hero object.
- **Cassette shell** (`0 16px 22px -12px oklch(19% 0.025 268 / 0.6), 0 3px 6px -2px oklch(19% 0.025 268 / 0.4), inset 0 1px 0 oklch(100% 0 0 / 0.2)`): For the physical cassette.
- **Fold crease** (`inset 7px 0 7px -7px oklch(19% 0.025 268 / 0.45), inset -7px 0 7px -7px oklch(19% 0.025 268 / 0.45)`): Vertical spine crease; becomes top/bottom inset shadows on narrow screens.
- **Flap crease** (`inset 0 7px 7px -7px oklch(19% 0.025 268 / 0.3)`): Fold line above `.jcard__flap`.
- **Pressed key** (`0 2px 0 var(--ink)`): Small `kbd` keycap depth only.

### Named Rules
**The Layered Paper Rule.** Add shadows only when an object is physically lifted, folded, inset, or pressed; ordinary content containers stay flat with rules and color blocks.

## Shapes

The form language is rectangular coated paper with tiny radii and occasional cassette hardware. The main `.jcard`, `.btn`, `.cmd`, and format blocks use a 4px radius. Stamps, copy labels, and keyboard keycaps sit at 3px. Focus outlines use a 2px visual radius, tick boxes use a sharp 1px radius, and the terminal `.screen__glass` uses 6px.

The cassette is the exception: `.cassette` uses a 0.6rem shell radius, `.cassette__label` uses 0.3rem, and `.cassette__window` uses 0.35rem. The OS switch uses 999px pills because it is an interactive selector, not part of the paper shape vocabulary. Side letters and side blocks stay square.

## Components

### `.topbar`, `.brand`, and `.chip`
- **Shape:** No enclosing bar; navigation floats on tape blue. `.chip` is a square 1.3rem side marker.
- **Color:** Topbar text is card stock on tape blue. `.chip--a` is record red with ink text; `.chip--b` is card stock with tape-blue text.
- **Typography:** `.brand` uses Archivo at 125% stretch, 800 weight, `0.8125rem`, uppercase. Navigation links use 600 weight at `0.9375rem`.
- **State:** Navigation hover underlines only; no pill backgrounds.

### `.jcard`
- **Shape:** A 4px-radius unfolded card with three main columns plus a flap row.
- **Color:** Card stock surface, leader-yellow spine, record/yellow/blue `.bands`, and ruled-blue index lines.
- **Depth:** Uses the J-card lift shadow and fold/inset shadows only on spine and flap.
- **Behavior:** At `68rem` and below, the spine becomes horizontal and the card becomes a single-column stack.

### `.cassette`
- **Shape:** A rounded plastic shell (`0.6rem`) with a card-stock label, dark window, reel SVGs, yellow band, and trapezoid foot.
- **Color:** Ink shell, card label, record-red side square, leader-yellow band, tape-brown reels.
- **Motion:** `.cassette.is-playing .reel__hub` spins at `1.5s linear infinite` while typing runs.
- **Use:** Signature hero object only; do not turn it into a generic card component.

### `.btn`
- **Shape:** 4px radius, `0.8rem 1.2rem` padding.
- **Primary:** Tape-blue background, card-stock text, 650 weight, `1rem`.
- **Ink variant:** `.btn--ink` uses ink background, then tape-blue hover.
- **State:** Hover changes background over `0.2s` with `--ease`; active translates down 1px.

### `.cmd` + `.copy`
- **Shape:** `.cmd` is a 4px-radius dark command strip with overflow hidden. On narrow screens it stacks command and copy button vertically.
- **Color:** Ink background with card-stock mono text. `.copy` defaults to leader yellow with ink text; hover shifts to a lighter yellow; success becomes card stock; failure becomes record red.
- **Typography:** Command text uses Sometype Mono at `0.9375rem`, 500 weight, 1.5 line-height. `.cmd--lead` increases to `clamp(1rem, 0.9rem + 0.6vw, 1.25rem)` and 600 weight.
- **Behavior:** JavaScript appends copy buttons to every `.cmd`; the hidden hero label copy button is revealed when JS loads.

### `.os` and `.os__switch`
- **Shape:** A 2px ink-stroked pill with 3px internal padding; selected items are 999px pills.
- **Color:** Unselected state is transparent. Hover uses 10% ink wash. Selected state is ink with card-stock text.
- **Typography:** Selector labels use 650 weight at `0.9375rem`.
- **Behavior:** Hidden without JS. The selected OS is stored in `localStorage` and filters `[data-os]` content.

### `.ticks`
- **Shape:** Inline radio labels with 0.9rem square tick boxes, 1.5px ink stroke, and 1px radius.
- **Color:** Card-stock box fill, leader-yellow hover, red-ink crossed checked mark.
- **Typography:** Labels use Archivo at 78% stretch, 600 weight, `0.8125rem`, 0.02em tracking.
- **Behavior:** Changing a style retypes the example title unless reduced motion is active.

### `.tracks` and `.track`
- **Shape:** Ordered tape-track rows with a 2px ink top rule and a two-column grid.
- **Color:** Record-red-ink track numbers; bonus track number uses leader-yellow block with ink text.
- **Typography:** Track numbers reuse printed-label logic; titles use the Title role.
- **Use:** Main instructional sequences only. Keep them linear and scannable.

### `.chart`, `.accepts`, `.styles`, and `.formats`
- **Shape:** Ruled lists and tables, not bordered cards. Borders use ruled blue or ink for header emphasis.
- **Color:** Text stays ink; headers often use ink-soft or yellow-section variants.
- **Typography:** Headers use condensed printed labels; file names use mono.
- **Responsive:** `.chart--files` hides the table head and becomes stacked blocks below `40rem`.

### `.screen`
- **Shape:** A 6px-radius dark terminal sketch, not a screenshot.
- **Color:** Ink background, soft light text, card-stock title, leader-yellow selected menu row.
- **Typography:** Sometype Mono at `0.875rem`, 1.75 line-height.
- **Use:** For terminal app sketches only; captions must clarify whether an image is a sketch or real screenshot.

### `.stamp`, `.bands`, `.stripes`, and side letter blocks
- **Shape:** `.stamp` is a 2px red-ink outlined label rotated -1.5 degrees. `.bands` and `.stripes` are horizontal package marks. `.side__letter` blocks are square identifiers.
- **Color:** Stamps use record-red-ink; package bands use record red, leader yellow, and tape blue/card stock.
- **Use:** These are packaging devices, not decoration. Use them to label state, side, or package structure.

## Do's and Don'ts

### Do:
- **Do** keep blue as the page field and white card stock as the primary reading surface.
- **Do** reserve Sometype Mono for commands, filenames, generated example output, SRT, terminal sketches, and other typed material.
- **Do** label any made-up episode or generated example as an example.
- **Do** extend instruction flows with `.tracks` and Side A / Side B language when the new content is procedural.
- **Do** honor `prefers-reduced-motion` by keeping content present without animation and disabling reel/typing transitions.
- **Do** use ruled lines, fold creases, side blocks, stamps, and package stripes before inventing new decorative motifs.

### Don't:
- **Don't** replace the hero with a dark terminal panel, dashboard shell, or feature-card grid.
- **Don't** use the older `src/Blazor/wwwroot/css/app.css` look as part of this public-site design system; it is out of scope.
- **Don't** introduce new accent hues unless the four package colors cannot solve the communication problem.
- **Don't** use Sometype Mono for broad body copy, display headlines, or navigation.
- **Don't** present invented output as real user data, real testimonials, benchmarks, or a real episode.
- **Don't** add generic shadows to every card; depth must map to a physical fold, lift, inset, or press.
