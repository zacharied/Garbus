*This document is a human-written work in progress.*

*The contents of this document are expected to differ from implementation.*

# Charts spec

All paths are relative to the song's root folder.

## Song

A song is a collection of charts associated with a single audio track.

### Data storage

Songs have the following metadata. All fields are to be in the source work's original written language unless otherwise specified.

#### Detail metadata

| Name            | type | Purpose                                            |
|-----------------|------|----------------------------------------------------|
| Title           | text | The title of the song.                             |
| Artist          | text | The artist of the song.                            |
| TitleRomanized  | text | The title of the song, romanized.                  |
| ArtistRomanized | text | The artist of the song, romanized.                 |
| Source          | text | The source of the song (other games, albums, etc.) |

#### Resource metadata

| Name   |Type| Purpose                                |
|--------|-|----------------------------------------|
| Track  |Path + audio file| The audio file for charts of this song |
| Jacket |Path + image file| The square jacket image for this song  |

## Chart

A chart defines hit object data.

### Levels

Levels are integers. A level of zero is represented as `??` in presentation. Negative levels are not allowed.

### Difficulties

The difficulty of a chart is a semantic representation of a chart's difficulty relative to the other charts for its song.

| Name     |
|----------|
| Tutorial |
| Novice   |
| Advanced |
| Expert   |

### Data storage

#### Detail metadata

| Name       | Type            | Purpose                                                                                            |
|------------|-----------------|----------------------------------------------------------------------------------------------------|
| ChartName  | Text (nullable) | The displayed name of the chart                                                                    |
| Charter    | Text            | The author of the chart                                                                            |
| Level      | Level           | A numeric difficulty level assigned by the charter                                                 |
| Difficulty | Difficulty      | The difficulty of the chart, used to identify difficulty gradation across charts for the same song |