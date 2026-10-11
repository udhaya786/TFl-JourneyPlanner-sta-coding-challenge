@manual @ignore
Feature: Additional scenarios (documented, not automated)
  Further functional and non-functional scenarios identified for the TfL Journey Planner
  and the Open Library Books API. They are written in Gherkin to show intended coverage
  and are tagged @ignore so they are listed as "not executed" and never fail the run.

  # ──────────────────────────── FUNCTIONAL – TfL Journey Planner ────────────────────────────

  @functional @web
  Scenario: Plan a journey using a postcode, address or landmark
    Given I am on the TfL Plan a journey page
    When I enter a postcode as the From location and a landmark as the To location
    And I plan my journey
    Then journey options between the two places should be displayed

  @functional @web
  Scenario: Same From and To location is rejected
    Given I am on the TfL Plan a journey page
    When I enter "Leicester Square Underground Station" in both the From and To fields
    And I plan my journey
    Then I should be told the journey cannot be planned between identical locations

  @functional @web
  Scenario: Swapping From and To reverses the journey
    Given I have planned a journey from "Leicester Square Underground Station" to "Covent Garden Underground Station"
    When I swap the From and To locations
    Then the results should show a journey from Covent Garden to Leicester Square

  @functional @web
  Scenario: Add a Via location
    Given I am on the TfL Plan a journey page
    When I plan a journey with a Via location
    Then each journey option should pass through the Via location

  @functional @web
  Scenario Outline: Plan a journey for different times
    Given I am on the TfL Plan a journey page
    When I plan a journey "<timeType>" at "<time>" on "<date>"
    Then the journey results should respect the chosen time

    Examples:
      | timeType    | time  | date        |
      | leaving     | 0915  | tomorrow    |
      | arriving by | 1730  | next Monday |
      | leaving     | 0600  | yesterday   |

  @functional @web
  Scenario Outline: Journey preferences change the results
    Given I have planned a journey from "Leicester Square Underground Station" to "Covent Garden Underground Station"
    When I choose the preference "<preference>"
    And I update the journey
    Then the results should reflect "<preference>"

    Examples:
      | preference                 |
      | Fastest routes             |
      | Routes with fewest changes |
      | Routes with least walking  |

  @functional @web
  Scenario: Step-free access preference
    Given I have planned a journey from "Leicester Square Underground Station" to "Covent Garden Underground Station"
    When I select full step-free access and update the journey
    Then only step-free routes should be shown, or a clear message if none exist

  @functional @web
  Scenario Outline: Transport mode filter
    Given I have planned a journey from "Leicester Square Underground Station" to "Covent Garden Underground Station"
    When I choose the journey type "<mode>"
    Then only "<mode>" options should be shown

    Examples:
      | mode       |
      | Bus only   |
      | Cycle hire |
      | Walking    |

  @functional @web
  Scenario: Autocomplete suggestions behave correctly
    Given I am on the TfL Plan a journey page
    When I type "lei" in the From field
    Then matching station suggestions should appear
    And I can choose a suggestion using the keyboard arrow keys and Enter
    When I type "zzzzzz" in the From field
    Then no suggestions should be shown

  @functional @web
  Scenario: Earlier and later journeys
    Given I have planned a journey from "Leicester Square Underground Station" to "Covent Garden Underground Station"
    When I choose "Later journeys"
    Then journeys departing after the original results should be shown

  @functional @web
  Scenario: Service disruption warning
    Given a line on the route is part-closed
    When I plan a journey that uses that line
    Then a disruption warning and sensible alternatives should be shown

  @functional @web
  Scenario: Save a journey to favourites
    Given I have planned a journey from "Leicester Square Underground Station" to "Covent Garden Underground Station"
    When I add the journey to favourites and reload the page
    Then the saved journey should be available to reuse

  @functional @web
  Scenario Outline: Planner works with cookie consent accepted or declined
    Given I am on the TfL Plan a journey page
    When I "<choice>" cookies
    And I plan a valid journey
    Then journey options should be displayed

    Examples:
      | choice  |
      | accept  |
      | decline |

  # ──────────────────────────── NON-FUNCTIONAL – TfL Journey Planner ────────────────────────────

  @non-functional @web
  Scenario: Keyboard-only operation (WCAG 2.2 AA)
    Given I am on the TfL Plan a journey page
    When I plan a journey using only the keyboard
    Then every control should be reachable, have a visible focus and be operable

  @non-functional @web
  Scenario: Screen reader support and error announcements
    Given I am using a screen reader on the Plan a journey page
    When I submit the form without entering any locations
    Then the From and To fields should have accessible names
    And the validation errors should be announced

  @non-functional @web
  Scenario: Colour contrast and zoom
    Given I am on the TfL Plan a journey page
    When I zoom the page to 200 percent
    Then all content should remain usable without horizontal scrolling
    And text and controls should meet WCAG AA contrast ratios

  @non-functional @web
  Scenario: Page and result performance
    Given I am on a normal broadband connection
    When I load the Plan a journey page and request a journey
    Then the page should be interactive within an agreed time
    And journey results should appear within an agreed time

  @non-functional @web
  Scenario: Performance on a slow network
    Given the network is throttled to 3G
    When I plan a journey
    Then the page should remain usable and show progress while loading

  @non-functional @web
  Scenario Outline: Responsive layout
    Given I open the Plan a journey page on a "<device>" viewport
    When I plan a journey
    Then the layout should be usable with no overlapping or cut-off content

    Examples:
      | device  |
      | phone   |
      | tablet  |
      | desktop |

  @non-functional @web
  Scenario Outline: Cross-browser compatibility
    Given I open the Plan a journey page in "<browser>"
    When I plan a valid journey
    Then the results should display correctly

    Examples:
      | browser  |
      | Chrome   |
      | Firefox  |
      | Safari   |
      | Edge     |

  @non-functional @web
  Scenario Outline: Unsafe input is handled safely
    Given I am on the TfL Plan a journey page
    When I enter "<input>" in the From field and plan my journey
    Then the input should not be executed or reflected unescaped
    And no stack trace or internal error details should be shown

    Examples:
      | input                      |
      | <script>alert(1)</script>  |
      | ' OR '1'='1                |

  @non-functional @web
  Scenario: Journey planner service unavailable
    Given the journey planner service is slow or unavailable
    When I plan a journey
    Then a clear, friendly error message should be shown rather than a blank page

  @non-functional @web
  Scenario: Peak load
    Given many users plan journeys at the same time during peak commuting hours
    When the service handles the concurrent requests
    Then the error rate should stay within the agreed limit
    And response times should stay within the agreed limit

  @non-functional @web
  Scenario: Privacy and cookie categories are respected
    Given I decline non-essential cookies
    When I use the Plan a journey page
    Then no non-essential cookies or trackers should be set

  # ──────────────────────────── FUNCTIONAL – Open Library Books API ────────────────────────────

  @functional @openlibrary
  Scenario: Request returns detailed data with jscmd=data
    Given I have book identifiers "ISBN:0201558025"
    When I send a GET request with the parameter jscmd=data
    Then the response should include the title, authors and publishers

  @functional @openlibrary
  Scenario: Duplicate identifiers return a single result
    Given I have book identifiers "ISBN:0201558025,ISBN:0201558025"
    When I send a GET request to the OpenLibrary Books API
    Then the number of returned books should be 1

  @functional @openlibrary
  Scenario: Malformed identifiers are handled gracefully
    Given I have book identifiers "ISBN:,:123,,"
    When I send a GET request to the OpenLibrary Books API
    Then the response code should be 200
    And no books should be returned

  @functional @openlibrary
  Scenario: Unsupported HTTP methods are rejected
    Given the OpenLibrary Books API is available
    When I send a POST request to the books endpoint
    Then the request should be rejected with an appropriate error status

  @non-functional @openlibrary
  Scenario: API response structure and headers
    Given I have book identifiers "ISBN:0201558025"
    When I send a GET request to the OpenLibrary Books API
    Then the response should match the expected JSON schema
    And the caching and CORS headers should be present and correct

  @non-functional @openlibrary
  Scenario: API behaviour under repeated requests
    Given I send many requests in a short period
    When the rate limit is reached
    Then the API should respond with a clear throttling status rather than failing silently
