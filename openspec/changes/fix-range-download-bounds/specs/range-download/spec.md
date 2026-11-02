## ADDED Requirements

### Requirement: Range bounds are inclusive and checked
`RangeLow` and `RangeHigh` SHALL be inclusive byte indexes. A `RangeHigh` past the end of the file
SHALL be clamped to the last byte. An empty or reversed range SHALL fail the download.

#### Scenario: High past the end
- **WHEN** the file has 1000 bytes and the range is 900..5000
- **THEN** the download completes with the last 100 bytes

#### Scenario: Reversed range
- **WHEN** `RangeHigh` is smaller than `RangeLow`
- **THEN** the download fails with `ArgumentOutOfRangeException`
