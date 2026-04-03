# Vision-first Product Definition

# Vision

This product is a **Polish news portal built around narrative comparison**, not around fact-checking, opinion publishing, or traditional aggregation.

Its purpose is to help readers quickly see **how the same current event is described by opposing ideological camps in Poland**, and to let them form their own view by exposing differences in framing, wording, emphasis, and narrative direction.

The experience should feel like a **10-minute coffee-break habit**: compact, engaging, readable, and visually clear.

It is not meant to be “the final truth.”  
It is meant to be a **transparent middle layer** between polarized media worlds.

# Core idea

The app should regularly collect fresh stories from a curated set of Polish media sources and identify cases where **the same event appears across both ideological camps**.

For each such story, the product should create:

- a **central refined synthesis** describing the shared event core in restrained, matter-of-fact language
- subtle **markers inside that synthesis** showing where wording, framing, or interpretation becomes especially revealing
- two clearly visible opposing perspectives:
  - **left / liberal / progressive**
  - **right / conservative / traditional**

When the user interacts with a marked phrase, the product should reveal how that part of the story is represented by each side, with direct citations and links to original sources.

So the product is not just “news in the middle.”  
It is **a structured comparison of how camps narrate the same reality**.

# Product identity

This should feel like:

- a **daily portal**
- a **clean reading experience**
- a **comparison tool for narrative differences**
- a **habit-forming overview of what happened in the last 12 hours**

It should not feel like:

- a fact-checking service
- a political scoring tool
- a trust-ranking engine
- a media watchdog shaming outlets
- a raw article aggregator

The tone should be calm, clear, and observational.

# Audience

The first audience is the **general Polish reader** who is interested in politics, society, or public affairs, but does not want to manually browse several ideologically different portals every day.

This includes people who:

- want a quick daily overview
- are curious how “the other side” frames the same story
- are tired of being trapped in one media bubble
- find comparison itself interesting and engaging
- want to make up their own mind rather than consume a prepackaged interpretation

The product should be understandable and attractive even to users who are not media analysts.

# Editorial promise

The promise is not:

> “We tell you the truth.”

The promise is:

> “We show you the common core of a story and how different camps frame it.”

This distinction is important.

The product should aim for:

- compression
- contrast
- clarity
- transparency

It should avoid claiming:

- full neutrality
- absolute objectivity
- factual arbitration between camps

The value comes from **making framing visible**, not from pretending framing can be fully eliminated.

# Source worldview model

For MVP, the world is intentionally simplified into **two camps**.

## Left camp

Includes sources associated with:

- left
- progressive
- liberal
- socialist

## Right camp

Includes sources associated with:

- right
- conservative
- traditional

This simplification is essential to the experience.

Even if reality is more nuanced, the first version should clearly stage the product as a comparison between two recognizable media blocs in Polish public life.

The final curated source list should define which outlet belongs to which side.  
That bucketing is a foundational editorial decision and should be treated as part of the product identity, not merely backend metadata.

# Source scope for MVP

MVP should focus on your **final curated list of general portals and news sources only**.

It should exclude:

- topic-based sources
- satire
- tabloids
- hybrid content sources that would distort tone or weaken the comparison

The source set is not just an input list.  
It defines:

- the quality of clustering
- the style of outputs
- the kind of polarization the product reveals
- the perceived fairness of the portal

Because of that, the source list should remain curated and intentional.

# What qualifies as a story worth showing

Not every scraped article deserves inclusion.

The product should highlight stories that are valuable **because they appear on both sides of the ideological divide**.

A story is especially worth surfacing when:

- it appears across multiple sources
- it is covered by both camps
- the event is recognizably the same
- the framing differs enough to make comparison meaningful

This is central to the vision.

The homepage should not simply show “important stories.”  
It should show **stories where cross-camp comparison is possible and interesting**.

That is what makes the product unique.

# Time model

The product is built around **fresh cycles**, not archive depth.

The intended rhythm is:

- regenerated every 12 hours
- focused on the current window
- no deep historical layer in MVP
- no requirement to build continuity from previous editions

If a story remains relevant across several cycles, it can keep reappearing.  
That is acceptable and even desirable, because recurrence itself signals continued media attention.

The product should therefore feel like:

- **morning edition**
- **evening edition**

rather than a timeless article library.

# Core user experience

## Homepage

The homepage should feel like a daily briefing built around comparison.

It should present a compact set of stories that best demonstrate:

- current relevance
- cross-camp coverage
- interesting narrative differences

One story may be treated as the strongest example of the edition — the one that best captures the idea of “how differently the same thing can be told.”

## Story page / story view

Each story should have a clear three-part structure:

### 1. Center

A refined synthesis:

- short
- readable
- calm
- stripped of overt ideological language
- focused on the shared event core

### 2. Embedded markers

Subtle phrase-level markers that indicate:

- loaded wording
- interpretive language
- moral framing
- narrative emphasis
- contested characterization

These should not overwhelm the text.  
They should reward curiosity.

### 3. Side reveal

On interaction, the left and right panels appear with:

- citations
- excerpts
- source names
- links to originals

This is where the product proves its value:  
the user sees not only that camps differ, but **how** they differ.

# Visual language

The camps should be **clearly visible** in the interface.

## Left

Use a reddish / pinkish styling.

## Right

Use a blue styling.

The center synthesis should remain visually calmer and more neutral than either side.

This color logic is not decorative.  
It reinforces the conceptual model:

- left view
- right view
- central synthesis

The UI should make that structure instantly understandable.

# Tone and writing principles

The middle synthesis should feel:

- concise
- restrained
- newspaper-like
- non-theatrical
- free from ideological shorthand

It should avoid:

- emotionally loaded verbs
- moral judgment
- speculative motives
- sarcastic or adversarial tone
- rhetorical phrasing

The goal is not to sound robotic, but to sound **measured**.

The side materials can preserve the tone of the original sources.  
That contrast is part of the product’s meaning.

# What the product is not trying to do

This is important because it affects many downstream decisions.

The MVP is **not** trying to:

- verify who is factually correct
- detect lies
- assign trust scores
- rank “better” or “worse” ideologies
- produce a centrist opinion
- resolve political disputes

It is also not trying to erase disagreement.

Instead, it should:

- preserve the existence of disagreement
- distinguish event core from interpretation
- let users see narrative differences without requiring them to manually compare outlets

This boundary should remain explicit in the product vision.

# What makes the product compelling

The main attraction is not neutrality by itself.

The attraction is:

- seeing the same event refracted through opposing lenses
- understanding how wording changes perception
- being able to compare without friction
- consuming current affairs in a more engaging way than by reading one portal

The emotional payoff is:

- curiosity
- recognition
- contrast
- better orientation in the media landscape

The usage pattern should feel like:

> “Let’s quickly see what happened — and how both sides are spinning it.”

That is stronger than a generic “daily digest.”

# Key product qualities to protect

These are the qualities that should guide all implementation decisions, even if the harness handles the technical side.

## 1. Clarity over completeness

It is better to show fewer stories well than many stories poorly.

## 2. Comparison over volume

The point is not quantity of articles.  
The point is meaningful contrast.

## 3. Readability over analytical overload

The product should stay inviting for normal readers, not only highly engaged political users.

## 4. Transparency over authority

The product should show its basis through citations and source links, rather than asking users to trust a hidden process.

## 5. Structure over chaos

The center/left/right model should be consistent and legible throughout the app.

## 6. Recency over depth

This is a live cyclical reading product, not a knowledge archive.

# Risks to be aware of at the vision level

These are not technical notes — they are product risks.

## Overclaiming neutrality

If the product presents itself as the definitive unbiased truth, it becomes fragile and easy to attack.

Better framing:

- common core
- synthesis
- comparison of narratives

## Over-annotating the central text

If too many phrases are marked, the product becomes exhausting and loses elegance.

## Showing stories that are not truly shared across camps

If the same-event comparison feels weak or forced, the core concept breaks.

## Allowing source mix to become too noisy

If the source set includes outlets too far from the intended editorial shape, the experience becomes less coherent.

## Making the side panels feel accusatory

The product should reveal framing differences, not lecture the user about media morality.

# MVP statement

A strong one-sentence version:

**A twice-daily Polish news portal that highlights stories covered by both left-leaning and right-leaning media, presents their shared core in a refined central synthesis, and reveals how each side frames the same event through cited excerpts and links.**

A shorter branding version:

**See what happened — and how both sides told it.**

# Final vision summary for the harness

Build a Polish news experience centered on **cross-camp narrative comparison**.

Use a curated set of left and right media sources.  
Look only at recent stories in a 12-hour cycle.  
Surface only stories that meaningfully appear across both camps.  
Present each story as a calm central synthesis with subtle interaction points that reveal how left and right sources frame the same event differently.  
Do not fact-check, do not rank trust, do not act as referee.  
The product’s value lies in making ideological framing visible, fast, and readable for an everyday user.
