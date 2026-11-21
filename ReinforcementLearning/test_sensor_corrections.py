import importlib.util
from pathlib import Path
import unittest
import numpy as np
from mlagents_envs.communicator_objects.agent_info_action_pair_pb2 import AgentInfoActionPairProto
from mlagents_envs.base_env import BehaviorSpec, ObservationSpec, DimensionProperty, ObservationType, ActionSpec
from mlagents_envs.rpc_utils import steps_from_proto

spec = importlib.util.spec_from_file_location('corrections', Path(__file__).with_name('collect-sensor-corrections.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class CorrectionTests(unittest.TestCase):
    def test_missing_or_aggregated_labels_are_rejected(self):
        samples = {'Correction/Command' + str(i): [(.1 * i, None)] for i in range(4)}
        np.testing.assert_allclose(module.teacher_label(samples), [0, .1, .2, .3])
        for bad in ({}, dict(samples, **{'Correction/Command0': [(0, None), (1, None)]})):
            with self.assertRaises(RuntimeError):
                module.teacher_label(bad)
        terminal_and_decision = {key: [(1, None)] + value for key, value in samples.items()}
        np.testing.assert_allclose(module.teacher_label(terminal_and_decision, True), [0, .1, .2, .3])

    def test_frame_uses_current_expert_label_and_preserves_input_contract(self):
        prototype = AgentInfoActionPairProto()
        for shape in ([3, 64, 84], [16]):
            prototype.agent_info.observations.add().shape.extend(shape)
        prototype.agent_info.observations[0].compression_type = 1
        label = np.array([.1, -.2, .3, -.4])
        observations = [np.full((3, 64, 84), .5), np.arange(16, dtype=np.float32) / 16]
        row = module.frame(prototype, observations, label, 2, False)
        np.testing.assert_allclose(row.action_info.continuous_actions, label)
        np.testing.assert_allclose(row.agent_info.observations[1].float_data.data, observations[1])
        self.assertTrue(row.agent_info.observations[0].compressed_data.startswith(b'\x89PNG'))
        self.assertEqual(len(prototype.action_info.continuous_actions), 0)
        contract = BehaviorSpec([
            ObservationSpec((3, 64, 84), (DimensionProperty.NONE, DimensionProperty.TRANSLATIONAL_EQUIVARIANCE,
                DimensionProperty.TRANSLATIONAL_EQUIVARIANCE), ObservationType.DEFAULT, 'rgb'),
            ObservationSpec((16,), (DimensionProperty.NONE,), ObservationType.DEFAULT, 'sensors')],
            ActionSpec.create_continuous(4))
        decision, _ = steps_from_proto([row.agent_info], contract)
        self.assertEqual(decision.obs[0].shape, (1, 3, 64, 84))
        np.testing.assert_allclose(decision.obs[0], 127 / 255, atol=1e-6)
        np.testing.assert_allclose(decision.obs[1][0], observations[1])


if __name__ == '__main__':
    unittest.main()
