# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

...

## [1.0.3] - 2022-06-22

- Bump UniversalTypeConverter from 2.0.0 to 2.6.0
- Update nupkg to target .net6 as well as .netstandard2.0
- Update unit test project to .Net 6

## [1.0.2] - 2020-09-16

- Added `ExplicitDateFormats` to GuessSettings

## [1.0.1] - 2020-07-06

### Fixed

- Fixed guessing and parsing for bigint / long values e.g. `"9223372036854775807"`

## [0.0.5] - 2019-11-01

### Fixed

- Fixed Exception message when giving Guesser mixed type input

## [0.0.4] - 2019-09-16

### Added

- Added GuessSettings class for specifying behaviour in potentially ambigious situations e.g. whether "Y"/"N" is accepted as boolean ("Y" = true and "N" = false)

## [0.0.3] - 2019-09-10

### Changed
- Improved performance of guessing decimals
- Decimal guesser trims trailing zeros (after NumberDecimalSeparator) unless scientific notation is being used

## [0.0.2] - 2019-08-30

### Fixed

- Fixed Unicode flag not being set in `Guesser.Guess`

## [0.0.1] - 2019-08-29

### Added

- Initial port of content from [FAnsiSql](https://github.com/HicServices/FAnsiSql)

[Unreleased]: https://github.com/HicServices/TypeGuesser/compare/1.0.3...develop
[1.0.3]: https://github.com/HicServices/TypeGuesser/compare/1.0.2...1.0.3
[1.0.2]: https://github.com/HicServices/TypeGuesser/compare/1.0.1...1.0.2
[1.0.1]: https://github.com/HicServices/TypeGuesser/compare/0.0.5...1.0.1
[0.0.5]: https://github.com/HicServices/TypeGuesser/compare/0.0.4...0.0.5
[0.0.4]: https://github.com/HicServices/TypeGuesser/compare/0.0.3...0.0.4
[0.0.3]: https://github.com/HicServices/TypeGuesser/compare/0.0.2...0.0.3
[0.0.2]: https://github.com/HicServices/TypeGuesser/compare/0.0.1...0.0.2
[0.0.1]: https://github.com/HicServices/TypeGuesser/compare/88b9b5d6622673eadc13c342f95c2e69ef760995...0.0.1
