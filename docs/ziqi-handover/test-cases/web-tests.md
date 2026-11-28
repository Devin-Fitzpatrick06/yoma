# Web test cases (Ziqi surfaces)

The `node --test` cases behind `pnpm test` for the Ziqi, My Yoma, badge and partner-panel code, listed by file.

134 test cases in 12 files.

## `src/web/src/api/services/partnerBadges.helpers.test.ts` (6)

- ids are unique, trimmed, lowercased and sorted
- the query key is the same whatever the order of the cards
- only earned levels show a chip
- the map looks organisations up case-insensitively
- the next quarterly review in words
- a missing or bad date still says how often

## `src/web/src/components/CourseQuality/DraftCheckPanel.helpers.test.ts` (16)

- partner wording for each predicted verdict
- an unknown verdict reads as not verified, never as verified
- the score is clamped for the ring
- every contract field has an entry
- fields map to the wizard
- go-to only for fields on the step on screen; other steps get a hint, never a jump
- a draft Ziqi cannot check shows the API
- only plain text suggestions can be applied
- a step
- an empty form is not checked
- the key ignores key order
- hard gates come first, otherwise the API
- a 404 means the API switch is off
- only the last value within the delay is sent
- a change aborts the request in flight and drops its answer
- errors reach onError; cancel stops everything

## `src/web/src/components/CourseQuality/DropOffSlot.model.test.ts` (8)

- Collecting never shows a number, even when a rate came along
- Available with a rate shows a whole percent
- Available without a usable rate falls back to Collecting
- missing drop-off or a blank note uses the standard note
- the client-side Collecting state has no rate
- clamps to 0–100%
- formatCount groups thousands and never goes negative
- the API note shows only when it adds to the drop-off note

## `src/web/src/components/CourseQuality/courseFeedbackFlow.test.ts` (11)

- Yes → step 1 → step 2 → submit sends the whole survey
- skipping at step 1 still records Taken with what was filled
- Not yet, No and dismiss send the answer with an empty survey
- a rating is a whole star from 1 to 5
- the comment is capped at 500 characters and blank becomes null
- next and back move only between the two survey steps
- at most one check a minute
- reset opens the gate again
- hidden on admin, organisation and auth routes
- shown on learner pages
- true when the document has an open dialog

## `src/web/src/components/CourseQuality/courseFeedbackSummaryModel.test.ts` (6)

- empty state when there is no summary or no answers
- ratings, finished and splits
- top tags, most picked first, as a share of answers
- comments only when asked for, blanks dropped
- a missing rating reads as none
- rounds the yes share

## `src/web/src/components/MyYoma/JourneyMap/journeyMap.test.ts` (20)

- orders stages by 
- an empty path has no steps
- is the first step not completed, in progress or not
- skips nothing out of order: a later completed step does not move the marker
- is past the last step when everything is done
- needs every step completed
- an empty stage is not complete
- nothing on the first render
- a step that became completed, and the stage it finished
- a swapped-in step that is already completed does not celebrate
- an unchanged path celebrates nothing
- practise when a stage holds an impact action or task
- prove for the last stage of a longer path, learn otherwise
- only earned partner levels show
- next steps: most common first, empty ones dropped, at most two
- drop-off slot: labelled once, never a number
- hours
- passes through every point
- an empty trail draws nothing
- fraction along the trail grows with the index and is clamped

## `src/web/src/components/MyYoma/firstVisit.test.ts` (20)

- asks everything when nothing came from preferences
- leaves out questions answered from preferences
- always asks the goal and starting point when there is no answer for them
- hides the goal when a saved answer exists (changing answers)
- the edit link shows every question again
- a country question without an 
- lists only questions that are actually hidden
- questions the API left out of the list still get the placeholders the request needs
- completes the request for hidden questions and keeps the youth
- an empty hidden country becomes anywhere (null)
- never invents a goal
- names each preference once in plain words
- is null without preferences
- keeps the current question when the list grows
- falls back to a valid index
- sends a signed-in youth without a path to My Yoma
- stays on the home page when ${name}
- the demo build always shows the preview
- development shows it only with the switch on
- production without the demo never shows it

## `src/web/src/components/YoID/MyPathCard.model.test.ts` (7)

- rounds and clamps
- labels steps done
- singular step, and completed never above total
- per-stage percent, done and a readable label
- no stages
- offset shrinks as the ring fills
- opportunityHref encodes the id

## `src/web/src/lib/courseGrading.test.ts` (23)

- weights normalise to shares of 100
- all-zero weights share equally
- weighted score is the weighted mean, rounded
- NotVerified and thin data are ungraded and left out of the percentiles
- percentile bands with cumulative shares
- a tied group on a cut-off goes to the lower band
- ties break on feedback before falling to the lower band
- a band
- NeedsReview reaches B at most
- NeedsReview cap follows the rubric
- A floors: syllabus drops to B
- A floors: feedback applies only with enough answers
- no feedback data: the feedback floor does not apply
- own shares from cumulative top percents, and back
- validation accepts the default rubric
- validation: band shares as the API checks them
- validation: weights, parameters and limits
- Ungraded: left out of the grades and the percentiles
- Ungraded does not touch verified courses
- not verified still reads as not verified
- validation accepts Ungraded and the A to D caps
- half rounds away from zero, despite float noise
- an exact .5 weighted score rounds up like the API

## `src/web/src/lib/courseQualityDemo.test.ts` (3)

- routes map to their side
- other routes have no side
- the partner link uses the configured organisation

## `src/web/src/lib/courseQualityReviewBatch.test.ts` (4)

- first batch: page 1 as is
- skipped courses never come back, even when they fill page 1
- only skipped courses left: an empty batch, nothing new
- a skipped course decided elsewhere is not counted

## `src/web/src/lib/pathway.test.ts` (10)

- a single-select replaces the answer, a multi-select toggles
- a single-select needs an answer; a multi-select may stay empty
- builds the request, mapping the anywhere option to a null country
- an incomplete or unknown answer gives no request
- prefills from the saved profile and drops keys no longer offered
- show only the rates the API returned, as whole percentages
- goal meta and progress
- next stage
- only A–D are grades
- badges: earned first, then by progress
