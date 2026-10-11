@api @functional
Feature: OpenLibrary Books API validations
  As a consumer of the OpenLibrary Books API
  I want book lookups by identifier to return correct data
  So that I can rely on the details and thumbnails it provides

  Background:
    Given the OpenLibrary Books API is available

  @non-functional
  Scenario: Successful GET request returns correct book details and thumbnails
    Given I have book identifiers "ISBN:0201558025,LCCN:93005405,ISBN:1583762027"
    When I send a GET request to the OpenLibrary Books API for those identifiers
    Then the response code should be 200
    And the response time should be less than 2000 milliseconds
    And the number of returned books should be 3
    And the response should contain correct details for each requested identifier
    And each returned book should include a thumbnail image URL
    And the thumbnail images should match the stored baseline images in the repository

  Scenario: Response is returned as JSON
    Given I have book identifiers "ISBN:0201558025"
    When I send a GET request to the OpenLibrary Books API for those identifiers
    Then the response code should be 200
    And the response content type should be JSON

  Scenario: Request with invalid book identifiers returns empty results
    Given I have book identifiers "INVALID:1234567"
    When I send a GET request to the OpenLibrary Books API for those identifiers
    Then the response code should be 200
    And the number of returned books should be 0

  Scenario: Request with mixed valid and invalid identifiers returns only valid results
    Given I have book identifiers "ISBN:0201558025,INVALID:999999"
    When I send a GET request to the OpenLibrary Books API for those identifiers
    Then the response code should be 200
    And the number of returned books should be 1
    And the response should contain correct details for identifier "ISBN:0201558025"

  Scenario: Request with no identifiers returns empty results
    Given I have book identifiers ""
    When I send a GET request to the OpenLibrary Books API for those identifiers
    Then the response code should be 200
    And the number of returned books should be 0
