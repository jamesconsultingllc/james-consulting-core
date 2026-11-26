Feature: Publish the package version calculated by NBGV
  Packages must be checked before signing or publishing.

  Scenario Outline: Accept a single package with the expected identity
    Given NBGV calculated version "<version>"
    When the only package is JamesConsulting with that version in its manifest and filename
    Then package verification succeeds

    Examples:
      | version                     |
      | 3.0.0-alpha.1.g0123456789    |
      | 3.0.0-rc.1                  |
      | 3.0.0                       |

  Scenario: Reject an old version hidden behind the expected filename
    Given NBGV calculated version "3.0.0"
    When JamesConsulting.3.0.0.nupkg contains version "2.0.0"
    Then package verification fails

  Scenario Outline: Reject unexpected artifacts
    Given NBGV calculated version "3.0.0"
    When the package directory contains "<artifacts>"
    Then package verification fails

    Examples:
      | artifacts                         |
      | no packages                       |
      | an extra stale package            |
      | a package with a different ID     |
      | a package with the wrong filename |
