@ui @functional
Feature: TfL Journey Planner widget
  As a visitor to the TfL website
  I want to plan a journey using the Plan a journey widget
  So that I can see travel options between two locations

  Background:
    Given I am on the TfL Plan a journey page
    And I have accepted cookies

  Scenario: Plan a valid journey and validate walking and cycling times
    When I enter "lei" in the From field and select "Leicester Square Underground"
    And I enter "cov" in the To field and select "Covent Garden Underground"
    And I plan my journey
    Then I should see the walking and cycling results
    And the walking time should be displayed
    And the cycling time should be displayed

  Scenario: Edit preferences to least walking and validate the journey time
    Given I have planned a journey from "Leicester Square Underground" to "Covent Garden Underground"
    When I edit my preferences to routes with least walking
    And I update the journey
    Then the updated journey should be displayed
    And the journey time should be displayed

  Scenario: View details and verify Covent Garden access information
    Given I have planned a journey from "Leicester Square Underground" to "Covent Garden Underground"
    When I edit my preferences to routes with least walking
    And I update the journey
    And I view the details of the first journey option
    Then the complete access information for "Covent Garden Underground Station" should be displayed

  Scenario Outline: Invalid locations return no results
    When I enter "<from>" in the From field without selecting a suggestion
    And I enter "<to>" in the To field without selecting a suggestion
    And I plan my journey
    Then I should see a message that no journey results were found

    Examples:
      | from                                  | to                                    |
      | 34                                    | 45                                    |
      | Leicester Square Underground Station  | 45                                    |
      | 34                                    | Covent Garden Underground Station     |

  Scenario: Cannot plan a journey when no locations are entered
    When I plan my journey
    Then I should see the From and To required errors
